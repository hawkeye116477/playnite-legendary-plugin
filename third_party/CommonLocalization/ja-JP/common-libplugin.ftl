common-launcher-custom-path = { $launcherName } のパス
common-games-installation-path = ゲームのインストール先
common-download-size = ダウンロードサイズ：
common-install-size = インストールサイズ：
common-uninstall-game-confirm = { $gameTitle } をアンインストールしてもよろしいですか？
common-download-settings = ダウンロード
common-unattended-install = 無人インストールモードを有効化
common-unattended-install-tool-tip = インストーラー画面で何も選択する必要がなくなり、必要な情報のダウンロード後にインストールが自動的に開始されます。
common-max-workers = 並列タスクの最大数
common-max-shared-memory = 共有メモリの最大量（MB）
common-connection-timeout = 接続タイムアウト（秒）
common-space-available = 利用可能な空き容量：
common-after-installing = インストール後：
common-verifying = 検証中...
common-verifying-large-file = 大きなファイル「{ $fileName }」を検証中：{ $progress }
common-downloading-update = 更新をダウンロード中....
common-finishing-installation = インストールを完了中...
common-finishing-update = 更新を完了中...
common-finishing-repair = 修復を完了中...
common-sync-error = { $gameTitle } のセーブデータをクラウドに同期中にエラーが発生しました。
common-import-installed-game = インストール済みゲームをインポート
common-importing-game = { $gameTitle } をインポート中...
common-import-finished = インポートが完了しました。楽しんでください。
common-repair = 修復
common-advanced-options = 詳細オプション
common-cache = キャッシュ
common-clear-cache = キャッシュをクリア
common-auto-clear-cache = キャッシュを自動的にクリア
common-clear-cache-confirm = キャッシュをクリアしてもよろしいですか？
common-check-log = 詳細についてはログファイルを確認してください。
common-task = タスク
common-download-properties = ダウンロードのプロパティ
common-migration-completed = 移行が正常に完了しました。
common-migration-no-games = 移行対象のゲームが見つかりません。
common-sign-out = サインアウト
common-troubleshooting-information = トラブルシューティング情報
common-playnite-version = Playnite バージョン
common-plugin-version = プラグインバージョン
common-launcher-version = { $launcherName } バージョン
common-launcher-binary = { $launcherName } バイナリ
common-copy-raw-data-to-clipboard = 生データをクリップボードにコピー
common-log-files-path = ログファイルのパス
common-report-bug = バグを報告
common-new-version-available = 新しいバージョンが利用可能です：{ $appName } { $appVersion }。
common-no-updates-available = 利用可能な更新はありません。
common-view-changelog = 変更履歴を表示
common-disable-game-update-checking = 更新チェックを無効化
common-startup-arguments = 起動引数
common-launcher-settings = 起動設定を変更
common-alternative-exe = 代替実行ファイル
common-check-all-content = すべてチェック
common-move = 移動
common-move-confirm = { $appName } を「{ $path }」に移動してもよろしいですか？これには時間がかかる場合があります。
common-moving-game = { $appName } を「{ $path }」に移動中...
common-move-game-success = { $appName } は「{ $path }」に正常に移動されました。
common-move-game-error = { $appName } を「{ $path }」に移動中にエラーが発生しました。
common-auto-update = ゲームを自動更新
common-games-update-policy = ゲームの更新を確認
common-check-updates-every-playnite-startup = Playnite 起動時に毎回
common-remove-game-launch-settings = 起動設定を削除
common-signed-in-as = { $userName } としてサインイン中
common-sign-out-confirm = サインアウトしてもよろしいですか？
common-permission-error = ユーザーに指定されたパスへの書き込み権限がありません。権限を変更するか、Playnite を管理者として起動してください。
common-check-for-games-updates-button = ゲームの更新を確認
common-not-enough-space = ディスクの空き容量が不足しています。
common-checking-for-updates = 更新を確認中...
common-uninstall-success =
    { $count ->
        [one] { $appName } は正常にアンインストールされました。
       *[other] 次のゲームは正常にアンインストールされました：{ $appName }
    }
common-uninstall-error =
    { $count ->
       *[other] 次のゲームをアンインストールできません：{ $appName }
    }
common-games = ゲーム
common-launcher-update-policy = { $launcherName } の更新を確認
common-launcher-not-installed = { $launcherName } がインストールされていません。
common-panel = { $pluginShortName } パネル
common-download-already-exists =
    { $count ->
        [one] { $appName } はすでにダウンロードリストにあります。Menu => View またはサイドバーから { $pluginShortName } パネルを開くと、ダウンロード状況を確認できます。
       *[other] 次のゲームはすでにダウンロードリストにあります：{ $appName }。Menu => View またはサイドバーから { $pluginShortName } パネルを開くと、ダウンロード状況を確認できます。
    }
common-migrate-games-original = { $originalPluginShortName } プラグインからすべてのゲームを移行
common-migrating-games-original = { $originalPluginShortName } プラグインからすべてのゲームを移行中...
common-migration-confirm = すべてのゲームが { $originalPluginShortName } プラグインから削除され、{ $pluginShortName } に移動されます。実行してもよろしいですか？
common-revert-migrate-games = すべてのゲームを { $originalPluginShortName } プラグインに移行
common-revert-migrating-games = すべてのゲームを { $originalPluginShortName } プラグインに移行中...
common-games-updates-underway = { $updatesSourceName } のゲームの更新をダウンロード中です。
common-instance-notice = { $pluginShortName } のインスタンスで一度にインストール、インポート、移動できるアプリケーションは 1 つだけです。続行するためにダウンロードマネージャーのタスクを一時停止しますか？
common-reload = 再読み込み
common-reload-confirm = データを再読み込みしてもよろしいですか？{ $updatesSourceName } サーバー付近で地震が発生する可能性があります。
common-migrating-data = データを移行中... お待ちください。
common-finish-installation = ゲームのインストールを完了
common-no-finish-needed = インストールの完了が必要なゲームはありません。
