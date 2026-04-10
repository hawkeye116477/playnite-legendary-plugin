common-launcher-custom-path = Ścieżka do { $launcherName }
common-games-installation-path = Ścieżka instalacji gier
common-download-size = Rozmiar pobierania:
common-install-size = Rozmiar instalacji:
common-uninstall-game-confirm = Czy na pewno chcesz odinstalować program { $gameTitle }?
common-download-settings = Pobieranie
common-unattended-install = Włącz tryb instalacji nienadzorowanej
common-unattended-install-tool-tip = Nie będziesz niczego musiał wybierać w oknie instalatora, a instalacja rozpocznie się automatycznie po pobraniu wymaganych informacji.
common-max-workers = Maksymalna liczba równoległych zadań
common-max-shared-memory = Maksymalna ilość pamięci współdzielonej (MB)
common-connection-timeout = Limit czasu połączenia (sek)
common-space-available = Dostępne miejsce:
common-after-installing = Po zainstalowaniu:
common-verifying = Weryfikowanie...
common-verifying-large-file = Weryfikowanie dużego pliku "{ $fileName }": { $progress }
common-downloading-update = Pobieranie aktualizacji...
common-finishing-installation = Kończenie instalacji...
common-finishing-update = Kończenie aktualizacji...
common-finishing-repair = Kończenie naprawy...
common-sync-error = Wystąpił błąd podczas synchronizacji zapisów gry { $gameTitle } z chmurą.
common-import-installed-game = Importuj zainstalowaną grę
common-importing-game = Importowanie gry { $gameTitle }...
common-import-finished = Importowanie zakończone. Dobrej zabawy.
common-repair = Napraw
common-advanced-options = Opcje zaawansowane
common-cache = Pamięć podręczna
common-clear-cache = Wyczyść pamięć podręczną
common-auto-clear-cache = Automatycznie czyść pamięć podręczną
common-clear-cache-confirm = Czy na pewno chcesz wyczyścić pamięć podręczną?
common-check-log = Sprawdź pliki dziennika, aby uzyskać więcej informacji.
common-task = Zadanie
common-download-properties = Właściwości pobierania
common-migration-completed = Migracja ukończona pomyślnie.
common-migration-no-games = Nie znaleziono gier do migracji.
common-sign-out = Wyloguj się
common-troubleshooting-information = Informacje do rozwiązywania problemów
common-playnite-version = Wersja Playnite
common-plugin-version = Wersja wtyczki
common-launcher-version = Wersja { $launcherName }
common-launcher-binary = Plik binarny { $launcherName }
common-copy-raw-data-to-clipboard = Skopiuj nieprzetworzone dane do schowka
common-log-files-path = Ścieżka do plików log
common-report-bug = Zgłoś błąd
common-new-version-available = Dostępna jest nowa wersja: { $appName } { $appVersion }.
common-no-updates-available = Brak dostępnych aktualizacji.
common-view-changelog = Zobacz listę zmian
common-disable-game-update-checking = Wyłącz sprawdzanie aktualizacji
common-startup-arguments = Argumenty startowe
common-launcher-settings = Modyfikuj ustawienia uruchamiania
common-alternative-exe = Alternatywny plik wykonywalny
common-check-all-content = Zaznacz wszystko
common-move = Przenieś
common-move-confirm = Czy na pewno chcesz przenieść grę { $appName } do „{ $path }”? To może zająć sporo czasu.
common-moving-game = Przenoszenie gry { $appName } do „{ $path }”...
common-move-game-success = Gra { $appName } została pomyślnie przeniesiona do „{ $path }”.
common-move-game-error = Wystąpił błąd podczas przenoszenia gry { $appName } do „{ $path }”.
common-auto-update = Automatycznie aktualizuj gry
common-games-update-policy = Sprawdzaj aktualizacje gier
common-check-updates-every-playnite-startup = Przy każdym uruchomieniu Playnite
common-remove-game-launch-settings = Usuń ustawienia uruchamiania
common-signed-in-as = Zalogowano jako { $userName }
common-sign-out-confirm = Czy na pewno chcesz się wylogować?
common-permission-error = Użytkownik nie ma dostępu do zapisu w określonej ścieżce. Zmień uprawnienia lub uruchom Playnite jako administrator.
common-check-for-games-updates-button = Sprawdź aktualizacje gier
common-not-enough-space = Za mało wolnego miejsca na dysku.
common-checking-for-updates = Sprawdzanie aktualizacji...
common-uninstall-success =
    { $count ->
        [one] Program { $appName } został pomyślnie odinstalowany.
       *[other] Następujące gry zostały pomyślnie odinstalowane: { $appName }.
    }
common-uninstall-error =
    { $count ->
       *[other] Nie można odinstalować następujących gier: { $appName }.
    }
common-games = Gry
common-launcher-update-policy = Sprawdzaj aktualizacje { $launcherName }
common-launcher-not-installed = Program { $launcherName } nie jest zainstalowany.
common-panel = Panel { $pluginShortName }
common-download-already-exists =
    { $count ->
        [one] { $appName } jest już na liście pobierania. Możesz zobaczyć jego stan, uzyskując dostęp do panelu { $pluginShortName } za pośrednictwem Menu => Widok lub paska bocznego.
       *[other] Następujące gry są już na liście pobierania: { $appName }. Możesz zobaczyć stan pobierania, uzyskując dostęp do panelu { $pluginShortName } za pośrednictwem Menu => Widok lub paska bocznego.
    }
common-migrate-games-original = Migruj wszystkie gry z wtyczki { $originalPluginShortName }
common-migrating-games-original = Migrowanie wszystkich gier z wtyczki { $originalPluginShortName }...
common-migration-confirm = Wszystkie gry zostaną usunięte z wtyczki { $originalPluginShortName } i przeniesione do { $pluginShortName }. Czy na pewno chcesz to zrobić?
common-revert-migrate-games = Migruj wszystkie gry do wtyczki { $originalPluginShortName }
common-revert-migrating-games = Migrowanie wszystkich gier do wtyczki { $originalPluginShortName }...
common-games-updates-underway = Trwa pobieranie aktualizacji dla gier z { $updatesSourceName }.
common-instance-notice = Tylko jedna instancja { $pluginShortName } może instalować/importować/przenosić aplikacje jednocześnie. Czy chcesz wstrzymać zadania menedżera pobierania, aby kontynuować?
common-reload = Odśwież
common-reload-confirm = Czy na pewno chcesz odświeżyć dane? To może spowodować trzęsienie ziemi w pobliżu serwerów { $updatesSourceName }.
common-migrating-data = Migracja danych... Proszę czekać.
