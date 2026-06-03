legendary-launch-offline = 可能であればゲームをオフラインモードで起動
legendary-enable-offline-mode = オフラインモードを有効化
legendary-offline-mode = オフラインモード
legendary-http-only-download = HTTPS を無効化（LanCache などの CDN で有用）
legendary-enable-reordering = 並べ替え最適化を有効化
legendary-enable-reordering-tool-tip = ダウンロード中の RAM 要件を削減できます（一部のタイトルでは正常に動作しない場合があります）。
legendary-ignore-free-space = 空き容量を無視
legendary-ignore-free-space-tool-tip = 十分な空き容量がない場合でも中止しません。
legendary-memory-error = 現在の共有メモリキャッシュが必要量より小さくなっています：{ $currentMemory } < { $requiredMemory }。メモリ使用量を削減するために並べ替え最適化を有効にするか、共有メモリの最大量を増やしてください。
legendary-no-linked-account = リンクされた { $companyAccount } アカウントが見つかりません！ブラウザーからアカウントをリンクして、もう一度お試しください。
legendary-all-activated-ubisoft = すべてのゲームはすでに { $companyAccount } アカウントで有効化されています。
legendary-continue-activation = { $companyAccount } アカウントでゲームを有効化してもよろしいですか？
legendary-games-activate-success = すべてのゲームが { $companyAccount } アカウントで正常に有効化されました。
legendary-activate-games = ゲームを有効化
legendary-games-activate-failure = { $companyAccount } アカウントでゲームを有効化できませんでした。理由：{ $reason }。
legendary-no-available-ea-games = 利用可能な EA ゲームはありません。
legendary-activate-next-confirm = 次のゲームを有効化しますか？
legendary-ea-notice = 現在、どのゲームが有効化を必要としているか不明なため、すべての有効化を試みます。EA App が有効化ではなくゲームのインストールを求めてきた場合、そのゲームはすでに有効化済みであり、ダイアログは閉じてかまいません。
legendary-star-wars-message = フォースと共にあらんことを、パダワン。
legendary-import-third-party-launcher-games = { $thirdPartyLauncherName } が必要なゲームをインポート
legendary-third-party-launcher-import-warn = この種類のゲームには Legendary は必要なく、すべてが Legendary 経由でインストールできるわけではありません。Cloud and other services => { $thirdPartyLauncherName } => Activate games に移動してゲームを有効化し、その後 { $thirdPartyLauncherName } から直接インストールすることをおすすめします。
legendary-required-install-via-third-party-launcher-error =
    { $count ->
        [one] { $gameTitle } は { $thirdPartyLauncherName } から直接インストールする必要があります。
       *[other] 次のゲームは { $thirdPartyLauncherName } から直接インストールする必要があります：{ $gameTitle }
    }
legendary-install-prerequisites = 必要なコンポーネントをインストール（{ $prerequisiteName }）
legendary-required-install-of-third-party-launcher =
    { $count ->
        [one] { $gameTitle } を使用するには { $thirdPartyLauncherName } のインストールが必要です。Plugin settings => Cloud and other services => { $thirdPartyLauncherName } => Activate games に移動してゲームを有効化し、その後 { $thirdPartyLauncherName } から直接インストールすることをおすすめします。
       *[other] 次のゲームを使用するには { $thirdPartyLauncherName } のインストールが必要です：{ $gameTitle }。Plugin settings => Cloud and other services => { $thirdPartyLauncherName } => Activate games に移動してゲームを有効化し、その後 { $thirdPartyLauncherName } から直接インストールすることをおすすめします。
    }
legendary-installing-prerequisites = 前提条件をインストール中（{ $prerequisiteName }）...
legendary-authenticate-alternative-label = 別の方法で認証
legendary-alternative-auth-instructions-first = 1. Web ブラウザーで Epic Games Store にログインします。
legendary-alternative-auth-instructions-second = 2. 下の入力ボックスに表示されている認証コード URL に移動します。
legendary-alternative-auth-instructions-third = 3. 下の入力ボックスに "authorizationCode" の値を貼り付け、"authenticate" ボタンをクリックします。
legendary-disable = 無効化
legendary-enable = 有効化
legendary-game-import-failure = ゲームをインポートできませんでした。理由：{ $reason }。
legendary-language-code = 言語（RFC 1766 形式のコード）
legendary-check-all-dlcs = すべての DLC を確認
legendary-launcher-update-source = Legendary の更新元
