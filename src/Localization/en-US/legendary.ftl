legendary-launch-offline = Launch games in offline mode if possible
legendary-enable-offline-mode = Enable offline mode
legendary-offline-mode = Offline mode
legendary-http-only-download = Disable HTTPS (useful for CDNs e.g. LanCache)
legendary-enable-reordering = Enable reordering optimization
legendary-enable-reordering-tool-tip = This can reduce RAM requirements during download (may not work properly for some titles).
legendary-ignore-free-space = Ignore free space
legendary-ignore-free-space-tool-tip = Do not abort if not enough free space is available.
legendary-memory-error = Current shared memory cache is smaller than required: { $currentMemory } < { $requiredMemory }. Try to enable reordering optimization to reduce memory usage or increase maximum amount of shared memory.
legendary-no-linked-account = No linked { $companyAccount } account found! Link your account via your browser and try again.
legendary-all-activated-ubisoft = All games have already been activated on your { $companyAccount } account.
legendary-continue-activation = Are you sure you want to activate games on your { $companyAccount } account?
legendary-games-activate-success = All games have been successfully activated on your { $companyAccount } account.
legendary-activate-games = Activate games
legendary-games-activate-failure = Failed to activate games on your { $companyAccount } account. Reason: { $reason }.
legendary-no-available-ea-games = No available EA Games.
legendary-activate-next-confirm = Do you want to activate next game?
legendary-ea-notice = It is currently unknown which game requires activation, so an attempt will be made to activate everything. If EA App asks you to install game rather than to activate, then it has already been activated and the dialog can be dismissed.
legendary-star-wars-message = May the Force be with you, Padawan.
legendary-import-third-party-launcher-games = Import games requiring { $thirdPartyLauncherName }
legendary-third-party-launcher-import-warn = These types of games doesn't require Legendary and not all of them can be installed via it. It's recommended to go to Additional services => { $thirdPartyLauncherName } => Activate games, and then install them directly via { $thirdPartyLauncherName }.
legendary-required-install-via-third-party-launcher-error =
    { $count ->
        [one] { $gameTitle } requires direct installation via { $thirdPartyLauncherName }.
       *[other] Following games require direct installation via { $thirdPartyLauncherName }: { $gameTitle }.
    }
legendary-install-prerequisites = Install prerequisites ({ $prerequisiteName })
legendary-required-install-of-third-party-launcher =
    { $count ->
        [one] { $gameTitle } requires installation of { $thirdPartyLauncherName }. It's recommended to go to Plugin settings => Additional services => { $thirdPartyLauncherName } => Activate games, and then install it directly via { $thirdPartyLauncherName }.
       *[other] Following games require installation of { $thirdPartyLauncherName }: { $gameTitle }. It's recommended to go to Plugin settings => Additional services => { $thirdPartyLauncherName } => Activate games, and then install them directly via { $thirdPartyLauncherName }.
    }
legendary-installing-prerequisites = Installing prerequisites ({ $prerequisiteName })...
legendary-authenticate-alternative-label = Authenticate using alternative method
legendary-alternative-auth-instructions-first = 1. Login to Epic Games Store in your web browser.
legendary-alternative-auth-instructions-second = 2. Navigate to authentication code URL, which is shown on the input box below.
legendary-alternative-auth-instructions-third = 3. Paste your "authorizationCode" value in the input box below and click on the "authenticate" button.
legendary-disable = Disable
legendary-enable = Enable
legendary-game-import-failure = Failed to import game. Reason: { $reason }.
legendary-language-code = Language (code in RFC 1766 format)
legendary-check-all-dlcs = Check all DLCs
legendary-launcher-update-source = Legendary update source
