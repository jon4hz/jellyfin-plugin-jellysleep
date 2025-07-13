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
  let isLoadingStatus = false;

  /**
   * Sleep timer options with their respective durations in minutes
   */
  const SLEEP_OPTIONS = {
    '15min': { label: '15 minutes', duration: 15 },
    '30min': { label: '30 minutes', duration: 30 },
    '1h': { label: '1 hour', duration: 60 },
    '2h': { label: '2 hours', duration: 120 },
    episode: { label: 'After this episode', duration: null },
  };

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
  function createSleepButton() {
    console.log('[Jellysleep DEBUG] Creating sleep button...');

    if (sleepButton) {
      console.log('[Jellysleep DEBUG] Sleep button already exists, returning existing button');
      return sleepButton;
    }

    // Create the sleep button to match Jellyfin's button structure
    sleepButton = document.createElement('button');
    sleepButton.setAttribute('is', 'paper-icon-button-light');
    sleepButton.className = 'btnJellysleep autoSize paper-icon-button-light';
    sleepButton.title = 'Sleep Timer';
    sleepButton.setAttribute('aria-label', 'Sleep Timer');
    sleepButton.innerHTML = `
            <span class="xlargePaperIconButton material-icons bedtime" aria-hidden="true"></span>
        `;

    console.log('[Jellysleep DEBUG] Sleep button element created');

    // Button click handler - show action sheet instead of custom menu
    sleepButton.addEventListener('click', e => {
      console.log('[Jellysleep DEBUG] Sleep button clicked');
      e.stopPropagation();
      showSleepActionSheet();
    });

    return sleepButton;
  }

  /**
   * Show the sleep action sheet using Jellyfin's native dialog system
   */
  function showSleepActionSheet() {
    console.log('[Jellysleep DEBUG] showSleepActionSheet called');

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
      if (isActive && currentTimerType === key && !isLoadingStatus) {
        const itemAside = document.createElement('div');
        itemAside.className = 'listItemAside actionSheetItemAsideText';
        itemAside.innerHTML = '<span class="material-icons" style="font-size: 1.2rem;">check</span>';
        menuItem.appendChild(itemAside);
      }

      menuItem.addEventListener('click', () => {
        if (isLoadingStatus) {
          console.log('[Jellysleep DEBUG] Ignoring click while loading status');
          return;
        }

        console.log(`[Jellysleep DEBUG] Action sheet item clicked: ${key}`);
        handleSleepOptionClick(key);
        closeSleepActionSheet();
      });

      scroller.appendChild(menuItem);
    });

    content.appendChild(scroller);
    dialog.appendChild(content);
    dialogContainer.appendChild(dialog);

    // Position the dialog near the button
    const buttonRect = sleepButton.getBoundingClientRect();
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
      if (!dialog.contains(e.target) && !sleepButton.contains(e.target)) {
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
    console.log('[Jellysleep DEBUG] closeSleepActionSheet called');

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

    // If clicking on the same option that's currently active, disable the timer
    if (isActive && currentTimerType === optionKey) {
      console.log('[Jellysleep DEBUG] Clicking on active timer option, disabling timer');
      cancelSleepTimer();
      return;
    }

    if (optionKey === 'episode') {
      startEpisodeTimer();
    } else {
      startDurationTimer(option.duration, option.label, optionKey);
    }
  }

  /**
   * Start a duration-based sleep timer
   */
  function startDurationTimer(minutes, label, timerType) {
    console.log(`[Jellysleep] Starting ${minutes} minute timer`);

    const endTime = new Date(Date.now() + minutes * 60 * 1000);
    sleepTimerEndTime = endTime;
    isActive = true;
    currentTimerType = timerType;

    // Call plugin API
    callPluginAPI('startTimer', {
      duration: minutes,
      endTime: endTime.toISOString(),
      type: 'duration',
      label: label,
    })
      .then(response => {
        console.log('[Jellysleep] Timer started successfully:', response);
        updateButtonAppearance();
        showNotification(`Sleep timer set for ${label}`);
      })
      .catch(error => {
        console.error('[Jellysleep] Failed to start timer:', error);
        // Reset state on error
        isActive = false;
        currentTimerType = null;
        sleepTimerEndTime = null;
        showNotification('Failed to start sleep timer');
        return;
      });
  }

  /**
   * Start episode-based sleep timer
   */
  function startEpisodeTimer() {
    console.log('[Jellysleep] Starting episode-based timer');

    isActive = true;
    currentTimerType = 'episode';

    // Call plugin API
    callPluginAPI('startTimer', {
      type: 'episode',
      label: 'After this episode',
    })
      .then(response => {
        console.log('[Jellysleep] Episode timer started successfully:', response);
        updateButtonAppearance();
        showNotification('Sleep timer set for end of episode');
      })
      .catch(error => {
        console.error('[Jellysleep] Failed to start episode timer:', error);
        // Reset state on error
        isActive = false;
        currentTimerType = null;
        showNotification('Failed to start sleep timer');
        return;
      });
  }

  /**
   * Cancel the active sleep timer
   */
  function cancelSleepTimer() {
    console.log('[Jellysleep] Cancelling sleep timer');

    if (sleepTimer) {
      clearTimeout(sleepTimer);
      sleepTimer = null;
    }

    sleepTimerEndTime = null;
    isActive = false;
    currentTimerType = null;

    // Call plugin API
    callPluginAPI('cancelTimer')
      .then(response => {
        console.log('[Jellysleep] Timer cancelled successfully:', response);
        updateButtonAppearance();
        showNotification('Sleep timer cancelled');
      })
      .catch(error => {
        console.error('[Jellysleep] Failed to cancel timer:', error);
        // Still update UI even if API call fails
        updateButtonAppearance();
        showNotification('Sleep timer cancelled (locally)');
      });
  }

  /**
   * Update button appearance based on timer state
   */
  function updateButtonAppearance() {
    if (!sleepButton) return;

    const icon = sleepButton.querySelector('.material-icons');

    if (isActive) {
      sleepButton.style.backgroundColor = 'rgba(0, 164, 220, 0.2)';
      icon.style.color = '#00a4dc';
      sleepButton.title = 'Sleep Timer Active - Click to manage';
    } else {
      sleepButton.style.backgroundColor = 'transparent';
      icon.style.color = '#fff';
      sleepButton.title = 'Sleep Timer';
    }
  }

  /**
   * Show a notification message
   */
  function showNotification(message) {
    // TODO: Use Jellyfin's notification system when available
    console.log(`[Jellysleep] Notification: ${message}`);

    // Simple toast notification
    const toast = document.createElement('div');
    toast.textContent = message;
    toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #333;
            color: #fff;
            padding: 1rem;
            border-radius: 4px;
            z-index: 10000;
            box-shadow: 0 4px 8px rgba(0,0,0,0.3);
        `;

    document.body.appendChild(toast);

    setTimeout(() => {
      if (toast.parentNode) {
        toast.parentNode.removeChild(toast);
      }
    }, 3000);
  }

  /**
   * Add the sleep button to the media player controls
   */
  function addSleepButtonToPlayer() {
    console.log('[Jellysleep DEBUG] Starting addSleepButtonToPlayer function');

    // Look for the buttons container in the osd controls
    const selectors = [
      '.osdControls .buttons', // Primary target: buttons container in osd controls
      '.videoOsdBottom .osdControls .buttons', // More specific path
      '.osdControls', // Fallback to osd controls container
      '.videoOsdBottom', // Fallback to video osd bottom
      '.headerButtons', // Header buttons fallback
    ];

    let controlsContainer = null;

    console.log('[Jellysleep DEBUG] Searching for control containers...');
    for (let i = 0; i < selectors.length; i++) {
      const selector = selectors[i];
      console.log(`[Jellysleep DEBUG] Trying selector ${i + 1}/${selectors.length}: "${selector}"`);
      controlsContainer = document.querySelector(selector);
      if (controlsContainer) {
        console.log(`[Jellysleep DEBUG] Found controls container with selector: "${selector}"`);
        console.log('[Jellysleep DEBUG] Container element:', controlsContainer);
        console.log('[Jellysleep DEBUG] Container children count:', controlsContainer.children.length);
        break;
      } else {
        console.log(`[Jellysleep DEBUG] Selector "${selector}" did not match any elements`);
      }
    }

    if (!controlsContainer) {
      console.log('[Jellysleep DEBUG] No player controls found, checking what elements are available...');

      // Debug: Check what's actually in the DOM
      const videoOsdBottom = document.querySelector('.videoOsdBottom');
      const osdControls = document.querySelector('.osdControls');
      const buttonsContainer = document.querySelector('.buttons');

      console.log('[Jellysleep DEBUG] .videoOsdBottom found:', !!videoOsdBottom);
      console.log('[Jellysleep DEBUG] .osdControls found:', !!osdControls);
      console.log('[Jellysleep DEBUG] .buttons found:', !!buttonsContainer);

      if (buttonsContainer) {
        console.log(
          '[Jellysleep DEBUG] .buttons container content preview:',
          buttonsContainer.innerHTML.substring(0, 200) + '...'
        );
      }

      console.log('[Jellysleep DEBUG] Retrying in 1 second...');
      setTimeout(addSleepButtonToPlayer, 1000);
      return;
    }

    // Check if button already exists
    if (controlsContainer.querySelector('.btnJellysleep')) {
      console.log('[Jellysleep DEBUG] Sleep button already exists, skipping injection');
      return;
    }

    console.log('[Jellysleep DEBUG] Creating sleep button...');
    const sleepButtonElement = createSleepButton();

    // Try to insert the button before the user rating button
    const userRatingBtn = controlsContainer.querySelector('.btnUserRating');
    if (userRatingBtn) {
      console.log('[Jellysleep DEBUG] Inserting sleep button before user rating button');
      userRatingBtn.insertAdjacentElement('beforebegin', sleepButtonElement);
    } else {
      console.log('[Jellysleep DEBUG] User rating button not found, appending to container');
      controlsContainer.appendChild(sleepButtonElement);
    }

    console.log('[Jellysleep DEBUG] Sleep button successfully added to player controls');
    console.log('[Jellysleep DEBUG] Button element:', sleepButtonElement);

    // Load initial timer status asynchronously after waiting for ApiClient
    waitForApiClient()
      .then(() => {
        loadTimerStatus();
      })
      .catch(error => {
        console.warn('[Jellysleep] Failed to wait for ApiClient, timer status will not be loaded:', error);
      });
  }

  /**
   * Initialize the plugin
   */
  function init() {
    console.log('[Jellysleep DEBUG] Initializing sleep timer plugin');
    console.log('[Jellysleep DEBUG] Document ready state:', document.readyState);
    console.log('[Jellysleep DEBUG] Current URL:', window.location.href);

    // Wait for ApiClient to be available
    waitForApiClient()
      .then(() => {
        console.log('[Jellysleep] ApiClient is ready, proceeding with initialization');

        // Wait for page to load
        if (document.readyState === 'loading') {
          console.log('[Jellysleep DEBUG] Document still loading, waiting for DOMContentLoaded');
          document.addEventListener('DOMContentLoaded', () => {
            console.log('[Jellysleep DEBUG] DOMContentLoaded fired, trying to add button');
            addSleepButtonToPlayer();
          });
        } else {
          console.log('[Jellysleep DEBUG] Document ready, immediately trying to add button');
          addSleepButtonToPlayer();
        }

        // Also try to add button when navigating (SPA behavior)
        const observer = new MutationObserver(mutations => {
          let shouldCheckForButton = false;

          mutations.forEach(mutation => {
            if (mutation.type === 'childList') {
              // Check if any added nodes contain video controls
              mutation.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) {
                  if (
                    node.classList &&
                    (node.classList.contains('videoOsdBottom') ||
                      node.classList.contains('osdControls') ||
                      node.classList.contains('buttons') ||
                      (node.querySelector &&
                        (node.querySelector('.videoOsdBottom') ||
                          node.querySelector('.osdControls') ||
                          node.querySelector('.buttons'))))
                  ) {
                    console.log('[Jellysleep DEBUG] Video controls detected in DOM mutation');
                    shouldCheckForButton = true;
                  }
                }
              });
            }
          });

          if (shouldCheckForButton) {
            console.log('[Jellysleep DEBUG] Scheduling button injection due to DOM changes');
            setTimeout(addSleepButtonToPlayer, 500);
          }
        });

        observer.observe(document.body, {
          childList: true,
          subtree: true,
        });

        console.log('[Jellysleep DEBUG] DOM observer started');

        // Also try periodically in case we miss the initial load
        let retryCount = 0;
        const retryInterval = setInterval(() => {
          retryCount++;
          console.log(`[Jellysleep DEBUG] Periodic retry attempt ${retryCount}/10`);

          if (document.querySelector('.btnJellysleep')) {
            console.log('[Jellysleep DEBUG] Button found, stopping periodic retries');
            clearInterval(retryInterval);
            return;
          }

          addSleepButtonToPlayer();

          if (retryCount >= 10) {
            console.log('[Jellysleep DEBUG] Max retry attempts reached, stopping periodic retries');
            clearInterval(retryInterval);
          }
        }, 2000);
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
          console.log('[Jellysleep] ApiClient is ready with access token');
          resolve();
          return;
        }

        retryCount++;
        if (retryCount >= maxRetries) {
          console.warn('[Jellysleep] ApiClient not available after waiting, proceeding anyway');
          reject(new Error('ApiClient not available'));
          return;
        }

        console.log(`[Jellysleep] Waiting for ApiClient... (${retryCount}/${maxRetries})`);
        setTimeout(checkApiClient, 1000);
      };

      checkApiClient();
    });
  }

  /**
   * Load current timer status from the API
   */
  async function loadTimerStatus() {
    console.log('[Jellysleep] Loading current timer status from API...');

    if (isLoadingStatus) {
      console.log('[Jellysleep] Already loading status, skipping...');
      return;
    }

    isLoadingStatus = true;

    try {
      // Wait for ApiClient to be available
      await waitForApiClient();

      const response = await callPluginAPI('status');
      console.log('[Jellysleep] Timer status response:', response);

      if (response && response.isActive) {
        isActive = true;
        currentTimerType = response.type;

        if (response.endTime) {
          sleepTimerEndTime = new Date(response.endTime);
        }

        updateButtonAppearance();
        console.log('[Jellysleep] Timer status loaded successfully');
      } else {
        console.log('[Jellysleep] No active timer found');
        isActive = false;
        currentTimerType = null;
        sleepTimerEndTime = null;
        updateButtonAppearance();
      }
    } catch (error) {
      console.error('[Jellysleep] Failed to load timer status:', error);
      // Don't change the current state on error, just log it
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
    isLoadingStatus: () => isLoadingStatus,
    loadStatus: loadTimerStatus,
    showActionSheet: showSleepActionSheet,
    closeActionSheet: closeSleepActionSheet,
    getButtonElement: () => sleepButton,
    debugMenu: () => {
      console.log('Button element:', sleepButton);
      console.log('Button in DOM:', document.contains(sleepButton));
      console.log('Is active:', isActive);
      console.log('Current timer type:', currentTimerType);
      console.log('Is loading status:', isLoadingStatus);
    },
  };
})();
