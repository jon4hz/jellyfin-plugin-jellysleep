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
  function createSleepButton() {
    // If button already exists, exit
    if (document.querySelector('.btnJellysleep')) {
      return;
    }

    // Create the sleep button to match Jellyfin's button structure
    sleepButton = document.createElement('button');
    sleepButton.setAttribute('is', 'paper-icon-button-light');
    sleepButton.className = 'btnJellysleep autoSize paper-icon-button-light';
    sleepButton.title = 'Sleep Timer';
    sleepButton.setAttribute('aria-label', 'Sleep Timer');
    sleepButton.innerHTML = `
            <span class="xlargePaperIconButton material-icons" aria-hidden="true">bedtime_off</span>
        `;

    // Button click handler - show action sheet instead of custom menu
    sleepButton.addEventListener('click', e => {
      e.stopPropagation();
      showSleepActionSheet();
    });

    return sleepButton;
  }

  /**
   * Show the sleep action sheet using Jellyfin's native dialog system
   */
  function showSleepActionSheet() {
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
    currentTimerType = timerType;

    // Call plugin API
    callPluginAPI('startTimer', {
      duration: minutes,
      endTime: endTime.toISOString(),
      type: 'duration',
      label: label,
    })
      .then(response => {
        updateButtonAppearance();
      })
      .catch(error => {
        // Reset state on error
        isActive = false;
        currentTimerType = null;
        sleepTimerEndTime = null;
        console.error('[Jellysleep] Failed to start duration timer:', error);
        return;
      });
  }

  /**
   * Start episode-based sleep timer
   */
  function startEpisodeTimer(episodeCount, label = 'After this episode') {
    isActive = true;
    currentTimerType = 'episode';

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
    if (!sleepButton) return;

    const icon = sleepButton.querySelector('.material-icons');
    if (!icon) return;

    if (isActive) {
      icon.textContent = 'bedtime';
      sleepButton.title = 'Sleep Timer - Active';
    } else {
      icon.textContent = 'bedtime_off';
      sleepButton.title = 'Sleep Timer';
    }
  }

  const isVideoPage = () => location.hash.startsWith('#/video');

  // if current page is a video page, add the sleep button to the player
  const updatePlayerUI = () => {
    if (isVideoPage()) {
      addSleepButtonToPlayer();
    }
  };

  /**
   * Add the sleep button to the media player controls
   */
  function addSleepButtonToPlayer() {
    // Check if the button already exists to avoid duplicates
    if (document.querySelector('.btnJellysleep')) {
      return;
    }
    const controlsContainer = document.querySelector('.videoOsdBottom .buttons.focuscontainer-x');
    if (!controlsContainer) {
      return;
    }

    const sleepButtonElement = createSleepButton();

    // Try to insert the button before the user rating button
    const userRatingBtn = controlsContainer.querySelector('.btnUserRating');
    if (userRatingBtn) {
      userRatingBtn.insertAdjacentElement('beforebegin', sleepButtonElement);
    } else {
      controlsContainer.appendChild(sleepButtonElement);
    }

    // Load initial timer status asynchronously after waiting for ApiClient
    waitForApiClient()
      .then(() => {
        loadTimerStatus();
      })
      .catch(error => {
        console.warn('[Jellysleep] Failed to wait for ApiClient, timer status will not be loaded:', error);
      });
  }

  // Monitor for changes in navigation and call updatePlayerUI
  const setupObserver = () => {
    const observer = new MutationObserver(() => {
      updatePlayerUI();
    });

    observer.observe(document.body, { childList: true, subtree: true, attributes: false });
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
      // Wait for ApiClient to be available
      await waitForApiClient();

      const response = await callPluginAPI('status');

      if (response && response.isActive) {
        isActive = true;
        currentTimerType = response.type;

        if (response.endTime) {
          sleepTimerEndTime = new Date(response.endTime);
        }

        updateButtonAppearance();
      } else {
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
