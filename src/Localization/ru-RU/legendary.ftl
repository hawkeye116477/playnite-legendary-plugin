legendary-launch-offline = Запуск игр в автономном режиме, если возможно
legendary-enable-offline-mode = Включить автономный режим
legendary-offline-mode = Автономный режим
legendary-preferred-cdn = Предпочтительный CDN
legendary-http-only-download = Отключить HTTPS (полезно для CDN, например, LanCache)
legendary-enable-reordering = Включить оптимизацию перестановки
legendary-enable-reordering-tool-tip = Это может уменьшить требования к ОЗУ во время загрузки (может работать неправильно для некоторых заголовков).
legendary-eos-overlay = Оверлей EOS
legendary-ignore-free-space = Игнорировать свободное место
legendary-ignore-free-space-tool-tip = Не закрывайте, если не хватает свободного места.
legendary-memory-error = Текущий общий объем кэша меньше требуемого: { $currentMemory } < { $requiredMemory }. Попробуйте включить оптимизацию перестановки для уменьшения использования памяти или увеличить максимальный объем общей памяти.
legendary-no-linked-account = Аккаунт { $companyAccount } не найден! Свяжите свой аккаунт через браузер и повторите попытку.
legendary-all-activated-ubisoft = Все игры уже активированы на вашем аккаунте { $companyAccount }.
legendary-continue-activation = Вы уверены, что хотите активировать игры на своей учетной записи { $companyAccount }?
legendary-games-activate-success = Все игры были успешно активированы на вашем аккаунте { $companyAccount }.
legendary-activate-games = Активировать игры
legendary-games-activate-failure = Не удалось активировать игры на вашей учетной записи { $companyAccount }. Причина: { $reason }.
legendary-no-available-ea-games = Нет доступных игр EA.
legendary-activate-next-confirm = Вы хотите активировать следующую игру?
legendary-ea-notice = В настоящее время неизвестно, какая игра требует активации, поэтому будет сделана попытка активировать все. Если EA App просит установить игру, а не активировать, значит, она уже была активирована и диалог можно пропустить.
legendary-star-wars-message = Да пребудет с тобой Сила, Padawan.
legendary-import-third-party-launcher-games = Импорт игр, требующих { $thirdPartyLauncherName }
legendary-third-party-launcher-import-warn = Эти игры не требуют Legendary и не все они могут быть установлены через него. Рекомендуется перейти к Дополнительные сервисы => { $thirdPartyLauncherName } => Активировать игры, а затем установить их непосредственно через { $thirdPartyLauncherName }.
legendary-required-install-via-third-party-launcher-error =
    { $count ->
        [one] { $gameTitle } требует прямой установки через { $thirdPartyLauncherName }.
       *[other] Следующие игры требуют прямой установки через { $thirdPartyLauncherName }: { $gameTitle }.
    }
legendary-install-prerequisites = Установите предварительные требования ({ $prerequisiteName })
legendary-required-install-of-third-party-launcher =
    { $count ->
        [one] { $gameTitle } Требует установки { $thirdPartyLauncherName }. Рекомендуется перейти в настройки плагина => Дополнительные сервисы => { $thirdPartyLauncherName } => Активировать игры, а затем установить его напрямую через { $thirdPartyLauncherName }.
       *[other] Следующие игры требуют установки { $thirdPartyLauncherName }: { $gameTitle }. Рекомендуется зайти в настройки плагина => Дополнительные сервисы => { $thirdPartyLauncherName } => Активировать игры, а затем установить их напрямую через { $thirdPartyLauncherName }.
    }
legendary-installing-prerequisites = Установка предварительных требований ({ $prerequisiteName })...
legendary-authenticate-alternative-label = Аутентификация с помощью альтернативного метода
legendary-alternative-auth-instructions-first = 1. Войдите в магазин Epic Games в вашем браузере.
legendary-alternative-auth-instructions-second = 2. Перейдите к коду аутентификации URL, который отображается в поле ввода ниже.
legendary-alternative-auth-instructions-third = 3. Вставьте ваше значение "authorizationCode" в поле ввода ниже и нажмите на кнопку "authenticate".
