/**
 * Jellysleep - Sleep Timer Plugin for Jellyfin
 * Adds sleep timer functionality to the media player
 */

(function () {
    'use strict';

    let sleepTimer = null;
    let sleepTimerEndTime = null;
    let sleepButton = null;
    let sleepMenu = null;
    let isActive = false;
    let currentTimerType = null;
    let currentTimerLabel = null;
    let isLoadingStatus = false;
    let countdownInterval = null;
    const CUSTOM_DURATION_DIALOG_ID = 'jellysleepCustomDurationDialog';

    /**
     * Sleep timer options with their respective durations in minutes
     */
    const SLEEP_OPTIONS = {
        '15min': { label: '15 minutes', duration: 15 },
        '30min': { label: '30 minutes', duration: 30 },
        '1h': { label: '1 hour', duration: 60 },
        '2h': { label: '2 hours', duration: 120 },
        episode: { label: 'After this episode', duration: null },
        custom: { label: 'Custom duration...', duration: null, custom: true },
    };

    function parseCustomDuration(input) {
        if (!input) {
            return null;
        }

        const value = input.trim().toLowerCase().replace(',', '.');

        // "1:30" means 1 hour 30 minutes
        const colonMatch = value.match(/^(\d+):(\d{1,2})$/);

        if (colonMatch) {
            const hours = parseInt(colonMatch[1], 10);
            const minutes = parseInt(colonMatch[2], 10);

            if (minutes >= 60) {
                return null;
            }

            return hours * 60 + minutes;
        }

        let totalMinutes = 0;

        const hourMatch = value.match(/(\d+(?:\.\d+)?)\s*h/);
        const minuteMatch = value.match(/(\d+)\s*(m|min|mins|minute|minutes)/);

        if (hourMatch) {
            totalMinutes += parseFloat(hourMatch[1]) * 60;
        }

        if (minuteMatch) {
            totalMinutes += parseInt(minuteMatch[1], 10);
        }

        // Plain number means minutes
        if (!hourMatch && !minuteMatch && /^\d+(?:\.\d+)?$/.test(value)) {
            totalMinutes = parseFloat(value);
        }

        totalMinutes = Math.round(totalMinutes);

        if (!Number.isFinite(totalMinutes) || totalMinutes < 1 || totalMinutes > 24 * 60) {
            return null;
        }

        return totalMinutes;
    }

    function formatRemaining(ms) {
        if (!ms || ms <= 0) {
            return '0:00';
        }

        const totalSeconds = Math.ceil(ms / 1000);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;

        if (hours > 0) {
            return `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
        }

        return `${minutes}:${String(seconds).padStart(2, '0')}`;
    }

    function startCountdownTicker() {
        if (countdownInterval) {
            return;
        }

        countdownInterval = setInterval(() => {
            if (isActive && sleepTimerEndTime && currentTimerType === 'duration') {
                const endTimeMs = sleepTimerEndTime instanceof Date
                    ? sleepTimerEndTime.getTime()
                    : new Date(sleepTimerEndTime).getTime();

                if (Date.now() >= endTimeMs) {
                    isActive = false;
                    sleepTimerEndTime = null;
                    currentTimerType = null;
                    currentTimerLabel = null;
                }
            }

            updateButtonAppearance();
        }, 1000);
    }

    function formatCustomDurationLabel(minutes) {
        if (minutes < 60) {
            return `${minutes} minutes`;
        }

        const hours = Math.floor(minutes / 60);
        const rest = minutes % 60;

        if (rest === 0) {
            return hours === 1 ? '1 hour' : `${hours} hours`;
        }

        return `${hours}h ${rest}m`;
    }

    function closeCustomDurationDialog() {
        const existingDialog = document.getElementById(CUSTOM_DURATION_DIALOG_ID);

        if (existingDialog) {
            existingDialog.remove();
        }
    }

    function showCustomDurationError(message) {
        const errorElement = document.querySelector(`#${CUSTOM_DURATION_DIALOG_ID} .jellysleepCustomDurationError`);

        if (errorElement) {
            errorElement.textContent = message;
            errorElement.style.display = 'block';
        }
    }

    function askAndStartCustomDuration() {
        closeCustomDurationDialog();

        const overlay = document.createElement('div');
        overlay.id = CUSTOM_DURATION_DIALOG_ID;

        overlay.style.position = 'fixed';
        overlay.style.left = '0';
        overlay.style.top = '0';
        overlay.style.right = '0';
        overlay.style.bottom = '0';
        overlay.style.zIndex = '999999';
        overlay.style.background = 'rgba(0, 0, 0, 0.65)';
        overlay.style.display = 'flex';
        overlay.style.alignItems = 'center';
        overlay.style.justifyContent = 'center';
        overlay.style.padding = '1.5em';

        const dialog = document.createElement('div');

        dialog.style.background = '#202020';
        dialog.style.color = '#fff';
        dialog.style.borderRadius = '0.7em';
        dialog.style.boxShadow = '0 0.5em 2em rgba(0, 0, 0, 0.6)';
        dialog.style.padding = '1.25em';
        dialog.style.width = '100%';
        dialog.style.maxWidth = '26em';
        dialog.style.boxSizing = 'border-box';

        dialog.innerHTML = `
        <div style="font-size: 1.25em; font-weight: 600; margin-bottom: 0.75em;">
            Custom sleep timer
        </div>

        <label style="display: block; font-size: 0.9em; opacity: 0.85; margin-bottom: 0.35em;">
            Duration
        </label>

        <input
            class="jellysleepCustomDurationInput"
            type="text"
            inputmode="text"
            autocomplete="off"
            value="45"
            placeholder="45, 90, 1:30, 1h 30m"
            style="
                width: 100%;
                box-sizing: border-box;
                padding: 0.85em;
                border-radius: 0.35em;
                border: 1px solid #666;
                background: #111;
                color: #fff;
                font-size: 1.1em;
                margin-bottom: 0.5em;
            "
        />

        <div style="font-size: 0.8em; opacity: 0.75; margin-bottom: 0.75em;">
            Examples: 45, 90, 1:30, 1h, 1h 30m, 1.5h
        </div>

        <div
            class="jellysleepCustomDurationError"
            style="
                display: none;
                color: #ff8080;
                font-size: 0.85em;
                margin-bottom: 0.75em;
            "
        ></div>

        <div style="display: flex; justify-content: flex-end; gap: 0.75em;">
            <button
                type="button"
                class="jellysleepCustomDurationCancel"
                style="
                    padding: 0.7em 1em;
                    border: 0;
                    border-radius: 0.35em;
                    background: transparent;
                    color: #fff;
                    font-size: 1em;
                "
            >
                Cancel
            </button>

            <button
                type="button"
                class="jellysleepCustomDurationStart"
                style="
                    padding: 0.7em 1em;
                    border: 0;
                    border-radius: 0.35em;
                    background: #00a4dc;
                    color: #fff;
                    font-size: 1em;
                    font-weight: 600;
                "
            >
                Start
            </button>
        </div>
    `;

        overlay.appendChild(dialog);
        document.body.appendChild(overlay);

        const input = dialog.querySelector('.jellysleepCustomDurationInput');
        const cancelButton = dialog.querySelector('.jellysleepCustomDurationCancel');
        const startButton = dialog.querySelector('.jellysleepCustomDurationStart');

        function submitCustomDuration() {
            const minutes = parseCustomDuration(input.value);

            if (!minutes) {
                showCustomDurationError('Invalid duration. Try for example: 45, 90, 1:30, 1h 30m.');
                input.focus();
                input.select();
                return;
            }

            closeCustomDurationDialog();
            startDurationTimer(minutes, formatCustomDurationLabel(minutes), 'custom');
        }

        cancelButton.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            closeCustomDurationDialog();
        });

        startButton.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            submitCustomDuration();
        });

        input.addEventListener('keydown', event => {
            if (event.key === 'Enter') {
                event.preventDefault();
                submitCustomDuration();
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeCustomDurationDialog();
            }
        });

        overlay.addEventListener('click', event => {
            if (event.target === overlay) {
                closeCustomDurationDialog();
            }
        });

        // Prevent clicks inside the dialog from closing Jellyfin's player controls
        dialog.addEventListener('click', event => {
            event.stopPropagation();
        });

        setTimeout(() => {
            input.focus();
            input.select();
        }, 100);
    }

    /**
     * Make API calls to the plugin backend
     */
    function callPluginAPI(action, data) {
        console.log(`[Jellysleep] API Call - Action: ${action}`, data);

        // Check if ApiClient is available
        if (!window.ApiClient || !window.ApiClient.accessToken || !window.ApiClient.accessToken()) {
            console.error('[Jellysleep] ApiClient not available or no access token');
            return Promise.reject(new Error('ApiClient not available'));
        }

        const baseUrl = window.ApiClient.serverAddress() || window.location.origin;

        switch (action) {
            case 'startTimer':
                return fetch(`${baseUrl}/Plugin/Jellysleep/StartTimer`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
                    },
                    body: JSON.stringify({
                        type: data.type,
                        duration: data.duration,
                        episodeCount: data.episodeCount,
                        endTime: data.endTime,
                        label: data.label,
                    }),
                })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error(`HTTP error! status: ${response.status}`);
                        }
                        return response.json();
                    })
                    .catch(error => {
                        console.error('[Jellysleep] Error starting timer:', error);
                        throw error;
                    });

            case 'cancelTimer':
                return fetch(`${baseUrl}/Plugin/Jellysleep/CancelTimer`, {
                    method: 'POST',
                    headers: {
                        Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
                    },
                })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error(`HTTP error! status: ${response.status}`);
                        }
                        return response.json();
                    })
                    .catch(error => {
                        console.error('[Jellysleep] Error cancelling timer:', error);
                        throw error;
                    });

            case 'status':
                return fetch(`${baseUrl}/Plugin/Jellysleep/Status`, {
                    headers: {
                        Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
                    },
                })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error(`HTTP error! status: ${response.status}`);
                        }
                        return response.json();
                    })
                    .catch(error => {
                        console.error('[Jellysleep] Error getting timer status:', error);
                        throw error;
                    });

            default:
                console.warn(`[Jellysleep] Unknown API action: ${action}`);
                return Promise.reject(new Error(`Unknown API action: ${action}`));
        }
    }

    /**
     * Create the sleep timer button
     */
    function createSleepButton(locationKey) {
        const existingButton = document.querySelector(`.btnJellysleep[data-jellysleep-location="${locationKey}"]`);

        if (existingButton) {
            return null;
        }

        const button = document.createElement('button');
        button.setAttribute('is', 'paper-icon-button-light');
        button.className = 'btnJellysleep autoSize paper-icon-button-light';
        button.dataset.jellysleepLocation = locationKey;
        button.title = 'Sleep Timer';
        button.setAttribute('aria-label', 'Sleep Timer');

        button.style.display = 'inline-flex';
        button.style.flexDirection = 'column';
        button.style.alignItems = 'center';
        button.style.justifyContent = 'center';
        button.style.lineHeight = '1';
        button.style.minWidth = '3.6em';

        button.innerHTML = `
        <span class="material-icons jellysleepIcon" aria-hidden="true">bedtime_off</span>
        <span class="jellysleepCountdown" aria-hidden="true"></span>
    `;

        button.addEventListener('click', e => {
            e.preventDefault();
            e.stopPropagation();

            // Use the clicked button as the menu anchor
            sleepButton = button;
            showSleepActionSheet(button);
        });

        return button;
    }
    /**
     * Show the sleep action sheet using Jellyfin's native dialog system
     */
    function showSleepActionSheet(anchorButton = sleepButton) {
        // Create backdrop
        const backdrop = document.createElement('div');
        backdrop.className = 'dialogBackdrop dialogBackdropOpened';
        backdrop.style.cssText = 'z-index: 1000;';

        // Create dialog container
        const dialogContainer = document.createElement('div');
        dialogContainer.className = 'dialogContainer';

        // Create the main dialog
        const dialog = document.createElement('div');
        dialog.className = 'focuscontainer dialog actionsheet-not-fullscreen actionSheet centeredDialog opened';
        dialog.setAttribute('data-history', 'true');
        dialog.setAttribute('data-removeonclose', 'true');
        dialog.style.cssText = 'animation: 140ms ease-out both scaleup; position: fixed; margin: 0px;';

        // Create action sheet content
        const content = document.createElement('div');
        content.className = 'actionSheetContent';

        const scroller = document.createElement('div');
        scroller.className = 'actionSheetScroller scrollY';

        if (isActive) {
            const cancelMenuItem = document.createElement('button');
            cancelMenuItem.setAttribute('is', 'emby-button');
            cancelMenuItem.setAttribute('type', 'button');
            cancelMenuItem.className = 'listItem listItem-button actionSheetMenuItem emby-button';
            cancelMenuItem.setAttribute('data-id', 'cancelTimer');

            if (isLoadingStatus) {
                cancelMenuItem.disabled = true;
                cancelMenuItem.style.opacity = '0.5';
            }

            const cancelItemBody = document.createElement('div');
            cancelItemBody.className = 'listItemBody actionsheetListItemBody';

            const cancelItemText = document.createElement('div');
            cancelItemText.className = 'listItemBodyText actionSheetItemText';
            cancelItemText.textContent = isLoadingStatus ? 'Cancel timer (Loading...)' : 'Cancel timer';

            cancelItemBody.appendChild(cancelItemText);
            cancelMenuItem.appendChild(cancelItemBody);

            cancelMenuItem.addEventListener('click', () => {
                if (isLoadingStatus) {
                    return;
                }

                cancelSleepTimer();
                closeSleepActionSheet();
            });

            scroller.appendChild(cancelMenuItem);
        }

        // Add menu items
        Object.keys(SLEEP_OPTIONS).forEach(key => {
            const option = SLEEP_OPTIONS[key];
            const menuItem = document.createElement('button');
            menuItem.setAttribute('is', 'emby-button');
            menuItem.setAttribute('type', 'button');
            menuItem.className = 'listItem listItem-button actionSheetMenuItem emby-button';
            menuItem.setAttribute('data-id', key);

            // Disable items while loading status
            if (isLoadingStatus) {
                menuItem.disabled = true;
                menuItem.style.opacity = '0.5';
            }

            const itemBody = document.createElement('div');
            itemBody.className = 'listItemBody actionsheetListItemBody';

            const itemText = document.createElement('div');
            itemText.className = 'listItemBodyText actionSheetItemText';
            itemText.textContent = isLoadingStatus ? `${option.label} (Loading...)` : option.label;

            itemBody.appendChild(itemText);
            menuItem.appendChild(itemBody);

            // Add checkmark for active timer
            if (isActive && currentTimerLabel === option.label && !isLoadingStatus) {
                const itemAside = document.createElement('div');
                itemAside.className = 'listItemAside actionSheetItemAsideText';
                itemAside.innerHTML = '<span class="material-icons" style="font-size: 1.2rem;">check</span>';
                menuItem.appendChild(itemAside);
            }

            menuItem.addEventListener('click', () => {
                if (isLoadingStatus) {
                    return;
                }

                handleSleepOptionClick(key);
                closeSleepActionSheet();
            });

            scroller.appendChild(menuItem);
        });

        content.appendChild(scroller);
        dialog.appendChild(content);
        dialogContainer.appendChild(dialog);

        // Position the dialog near the button
        const buttonRect = anchorButton.getBoundingClientRect();
        dialog.style.left = Math.max(10, buttonRect.left - 100) + 'px';
        dialog.style.top = Math.max(10, buttonRect.top - 200) + 'px';

        // Add to DOM
        document.body.appendChild(backdrop);
        document.body.appendChild(dialogContainer);

        // Store references for cleanup
        sleepMenu = { backdrop, dialogContainer, dialog };

        // Close on backdrop click
        backdrop.addEventListener('click', closeSleepActionSheet);

        // Close when clicking outside the dialog
        const outsideClickHandler = e => {
            if (!dialog.contains(e.target) && !anchorButton.contains(e.target)) {
                closeSleepActionSheet();
                document.removeEventListener('click', outsideClickHandler, true);
            }
        };
        // Use capture phase to catch clicks before they bubble up
        setTimeout(() => {
            document.addEventListener('click', outsideClickHandler, true);
        }, 0);

        // Close on escape key
        const escapeHandler = e => {
            if (e.key === 'Escape') {
                closeSleepActionSheet();
                document.removeEventListener('keydown', escapeHandler);
                document.removeEventListener('click', outsideClickHandler, true);
            }
        };
        document.addEventListener('keydown', escapeHandler);

        // Store event handlers for cleanup
        sleepMenu.outsideClickHandler = outsideClickHandler;
        sleepMenu.escapeHandler = escapeHandler;
    }

    /**
     * Close the sleep action sheet
     */
    function closeSleepActionSheet() {
        if (sleepMenu) {
            // Clean up event listeners
            if (sleepMenu.outsideClickHandler) {
                document.removeEventListener('click', sleepMenu.outsideClickHandler, true);
            }
            if (sleepMenu.escapeHandler) {
                document.removeEventListener('keydown', sleepMenu.escapeHandler);
            }

            // Remove DOM elements
            if (sleepMenu.backdrop) {
                sleepMenu.backdrop.remove();
            }
            if (sleepMenu.dialogContainer) {
                sleepMenu.dialogContainer.remove();
            }

            sleepMenu = null;
        }
    }

    /**
     * Handle sleep option selection
     */
    function handleSleepOptionClick(optionKey) {
        const option = SLEEP_OPTIONS[optionKey];

        if (!option) {
            return;
        }

        if (option.custom) {
            askAndStartCustomDuration();
            return;
        }

        // If clicking on the same option that's currently active, disable the timer
        if (isActive && currentTimerLabel === option.label) {
            cancelSleepTimer();
            return;
        }

        if (optionKey === 'episode') {
            startEpisodeTimer(1);
        } else if (option.episodeCount) {
            startEpisodeTimer(option.episodeCount, option.label);
        } else {
            startDurationTimer(option.duration, option.label, optionKey);
        }
    }

    /**
     * Start a duration-based sleep timer
     */
    function startDurationTimer(minutes, label, timerType) {
        const endTime = new Date(Date.now() + minutes * 60 * 1000);

        sleepTimerEndTime = endTime;
        isActive = true;

        // Important: this must be "duration", not "15min", "30min", "custom", etc.
        currentTimerType = 'duration';

        currentTimerLabel = label;

        // Show countdown immediately, before waiting for backend response
        updateButtonAppearance();

        callPluginAPI('startTimer', {
            duration: minutes,
            endTime: endTime.toISOString(),
            type: 'duration',
            label: label,
        })
            .then(response => {
                isActive = true;
                currentTimerType = 'duration';
                currentTimerLabel = response && response.label ? response.label : label;

                if (response && response.endTime) {
                    sleepTimerEndTime = new Date(response.endTime);
                } else {
                    sleepTimerEndTime = endTime;
                }

                updateButtonAppearance();
            })
            .catch(error => {
                isActive = false;
                currentTimerType = null;
                currentTimerLabel = null;
                sleepTimerEndTime = null;

                console.error('[Jellysleep] Failed to start duration timer:', error);
                updateButtonAppearance();
            });
    }

    /**
     * Start episode-based sleep timer
     */
    function startEpisodeTimer(episodeCount, label = 'After this episode') {
        isActive = true;
        currentTimerType = 'episode';
        currentTimerLabel = label;

        // Call plugin API
        callPluginAPI('startTimer', {
            type: 'episode',
            episodeCount: episodeCount,
            label: label,
        })
            .then(response => {
                updateButtonAppearance();
            })
            .catch(error => {
                // Reset state on error
                isActive = false;
                currentTimerType = null;
                currentTimerLabel = null;
                console.error('[Jellysleep] Failed to start episode timer:', error);
                return;
            });
    }

    /**
     * Cancel the active sleep timer
     */
    function cancelSleepTimer() {
        if (sleepTimer) {
            clearTimeout(sleepTimer);
            sleepTimer = null;
        }

        sleepTimerEndTime = null;
        isActive = false;
        currentTimerType = null;
        currentTimerLabel = null;

        // Call plugin API
        callPluginAPI('cancelTimer')
            .then(response => {
                updateButtonAppearance();
            })
            .catch(error => {
                // Still update UI even if API call fails
                updateButtonAppearance();
                console.error('[Jellysleep] Failed to cancel sleep timer:', error);
            });
    }
    /**
     * Update button appearance based on timer state
     */
    function updateButtonAppearance() {
        const buttons = document.querySelectorAll('.btnJellysleep');

        if (!buttons.length) {
            return;
        }

        let countdownText = '';

        if (isActive) {
            if (sleepTimerEndTime) {
                const endTimeMs = sleepTimerEndTime instanceof Date
                    ? sleepTimerEndTime.getTime()
                    : new Date(sleepTimerEndTime).getTime();

                countdownText = formatRemaining(endTimeMs - Date.now());
            } else if (currentTimerType === 'episode') {
                countdownText = 'EP';
            }
        }

        buttons.forEach(button => {
            const icon = button.querySelector('.jellysleepIcon');
            const countdown = button.querySelector('.jellysleepCountdown');

            if (icon) {
                icon.textContent = isActive ? 'bedtime' : 'bedtime_off';
            }

            if (countdown) {
                countdown.textContent = countdownText;
                countdown.style.display = countdownText ? 'block' : 'none';
                countdown.style.fontSize = '0.58em';
                countdown.style.fontWeight = '700';
                countdown.style.marginTop = '0.15em';
                countdown.style.opacity = '0.95';
                countdown.style.minHeight = '0.8em';
            }

            button.title = isActive
                ? `Sleep Timer - Active${currentTimerLabel ? ': ' + currentTimerLabel : ''}${countdownText ? ' (' + countdownText + ')' : ''}`
                : 'Sleep Timer';

            button.setAttribute('aria-label', button.title);
        });
    }
    const isVideoPage = () => location.hash.startsWith('#/video');

    /**
     * Update all possible player UI locations.
     *
     * This keeps the original video page behavior, but also supports:
     * - video player fallback near repeat button
     * - opened now playing page
     * - minimized now playing bar
     */
    const updatePlayerUI = () => {
        if (isVideoPage()) {
            addSleepButtonToPlayer();
        }

        addSleepButtonToNowPlayingPage();
        addSleepButtonToNowPlayingBar();
    };

    /**
     * Add the sleep button to the opened now playing page
     */
    function addSleepButtonToNowPlayingPage() {
        const repeatButtons = Array.from(document.querySelectorAll(
            'button.repeatToggleButton[data-command="SetRepeatMode"], ' +
            'button.btnRepeat[data-command="SetRepeatMode"], ' +
            'button.repeatToggleButton, ' +
            'button.btnRepeat'
        ));

        const repeatButton = repeatButtons.find(button =>
            !button.closest('.nowPlayingBar') &&
            !button.closest('.videoOsdBottom') &&
            !button.closest('.videoOsd')
        );

        if (!repeatButton) {
            return;
        }

        const parent = repeatButton.parentElement;

        if (parent && parent.querySelector('.btnJellysleep')) {
            return;
        }

        const sleepButtonElement = createSleepButton('now-playing-page');

        if (!sleepButtonElement) {
            return;
        }

        repeatButton.insertAdjacentElement('afterend', sleepButtonElement);

        loadInitialTimerStatus();
    }
    /**
     * Add the sleep button to the video player controls
     */
    function addSleepButtonToPlayer() {
        const controlsContainer = document.querySelector('.videoOsdBottom .buttons.focuscontainer-x');

        if (controlsContainer) {
            if (controlsContainer.querySelector('.btnJellysleep')) {
                return;
            }

            const sleepButtonElement = createSleepButton('video-player');

            if (!sleepButtonElement) {
                return;
            }

            const userRatingButton = controlsContainer.querySelector('.btnUserRating');

            if (userRatingButton) {
                controlsContainer.insertBefore(sleepButtonElement, userRatingButton);
            } else {
                controlsContainer.appendChild(sleepButtonElement);
            }

            loadInitialTimerStatus();
            return;
        }

        // Only use this fallback on the actual video page.
        // Otherwise it can target the now-playing-page repeat button and create duplicates.
        if (!isVideoPage()) {
            return;
        }

        const repeatButton =
            document.querySelector('.videoOsdBottom button.repeatToggleButton[data-command="SetRepeatMode"]') ||
            document.querySelector('.videoOsdBottom button.btnRepeat[data-command="SetRepeatMode"]') ||
            document.querySelector('.videoOsdBottom button.repeatToggleButton') ||
            document.querySelector('.videoOsdBottom button.btnRepeat') ||
            document.querySelector('button.repeatToggleButton[data-command="SetRepeatMode"]') ||
            document.querySelector('button.btnRepeat[data-command="SetRepeatMode"]');

        if (!repeatButton) {
            return;
        }

        const parent = repeatButton.parentElement;

        if (parent && parent.querySelector('.btnJellysleep')) {
            return;
        }

        const sleepButtonElement = createSleepButton('video-player-repeat-fallback');

        if (!sleepButtonElement) {
            return;
        }

        repeatButton.insertAdjacentElement('afterend', sleepButtonElement);

        loadInitialTimerStatus();
    }

    /**
     * Add the sleep button to the now playing bar
     */
    function addSleepButtonToNowPlayingBar() {
        const nowPlayingBar = document.querySelector('.nowPlayingBar');

        if (!nowPlayingBar) {
            return;
        }

        const nowPlayingBarRight = nowPlayingBar.querySelector('.nowPlayingBarRight');

        if (!nowPlayingBarRight) {
            return;
        }

        const sleepButtonElement = createSleepButton('now-playing-bar');

        if (!sleepButtonElement) {
            return;
        }

        const userDataButtons = nowPlayingBarRight.querySelector('.nowPlayingBarUserDataButtons');

        if (userDataButtons) {
            userDataButtons.insertAdjacentElement('beforebegin', sleepButtonElement);
        } else {
            const contextMenuBtn = nowPlayingBarRight.querySelector('.btnToggleContextMenu');

            if (contextMenuBtn) {
                contextMenuBtn.insertAdjacentElement('beforebegin', sleepButtonElement);
            } else {
                nowPlayingBarRight.appendChild(sleepButtonElement);
            }
        }

        loadInitialTimerStatus();
    }

    /**
     * Load initial timer status after ApiClient is available
     */
    function loadInitialTimerStatus() {
        waitForApiClient()
            .then(() => {
                loadTimerStatus();
            })
            .catch(error => {
                console.warn('[Jellysleep] Failed to wait for ApiClient, timer status will not be loaded:', error);
            });
    }

    /**
     * Monitor for changes in navigation/player UI and call updatePlayerUI
     */
    const setupObserver = () => {
        const observer = new MutationObserver(() => {
            updatePlayerUI();
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: false
        });
    };

    /**
     * Initialize the plugin
     */
    function init() {
        // Wait for ApiClient to be available
        waitForApiClient()
            .then(() => {
                setupObserver();
                updatePlayerUI();
                startCountdownTicker();
            })
            .catch(error => {
                console.error('[Jellysleep] ApiClient not available, initialization aborted:', error);
            });
    }

    /**
     * Wait for ApiClient to be available with authentication
     */
    function waitForApiClient() {
        return new Promise((resolve, reject) => {
            let retryCount = 0;
            const maxRetries = 30; // Wait up to 30 seconds

            const checkApiClient = () => {
                if (window.ApiClient && window.ApiClient.accessToken && window.ApiClient.accessToken()) {
                    resolve();
                    return;
                }

                retryCount++;
                if (retryCount >= maxRetries) {
                    reject(new Error('ApiClient not available'));
                    return;
                }

                setTimeout(checkApiClient, 1000);
            };

            checkApiClient();
        });
    }

    /**
     * Load current timer status from the API
     */
    async function loadTimerStatus() {
        if (isLoadingStatus) {
            return;
        }

        isLoadingStatus = true;

        try {
            await waitForApiClient();

            const response = await callPluginAPI('status');

            if (response && response.isActive) {
                isActive = true;
                currentTimerType = response.type;
                currentTimerLabel = response.label;

                if (response.endTime) {
                    sleepTimerEndTime = new Date(response.endTime);
                } else {
                    sleepTimerEndTime = null;
                }

                updateButtonAppearance();
            } else {
                isActive = false;
                currentTimerType = null;
                currentTimerLabel = null;
                sleepTimerEndTime = null;
                updateButtonAppearance();
            }
        } catch (error) {
            console.error('[Jellysleep] Failed to load timer status:', error);
        } finally {
            isLoadingStatus = false;
        }
    }

    // Initialize when script loads
    init();

    // Expose functions for debugging
    window.Jellysleep = {
        cancelTimer: cancelSleepTimer,
        isActive: () => isActive,
        getEndTime: () => sleepTimerEndTime,
        getCurrentType: () => currentTimerType,
        getCurrentLabel: () => currentTimerLabel,
        isLoadingStatus: () => isLoadingStatus,
        loadStatus: loadTimerStatus,
        showActionSheet: showSleepActionSheet,
        closeActionSheet: closeSleepActionSheet,
        getButtonElement: () => sleepButton,
        getButtonElements: () => Array.from(document.querySelectorAll('.btnJellysleep')),
        updatePlayerUI,
        debugMenu: () => {
            const buttons = Array.from(document.querySelectorAll('.btnJellysleep'));

            console.log('Last clicked button element:', sleepButton);
            console.log('All Jellysleep buttons:', buttons);
            console.log('Button count:', buttons.length);
            console.log('Is active:', isActive);
            console.log('Current timer type:', currentTimerType);
            console.log('Current timer label:', currentTimerLabel);
            console.log('End time:', sleepTimerEndTime);
            console.log('Is loading status:', isLoadingStatus);
            console.log('Custom dialog:', document.getElementById(CUSTOM_DURATION_DIALOG_ID));
        },
    };
})();
