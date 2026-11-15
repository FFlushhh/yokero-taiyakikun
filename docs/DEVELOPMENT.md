# 開発環境・セットアップ

本ドキュメントでは、「よけろ！たいやきくん」の開発環境、Unityプロジェクトの起動方法、自動テストの実行方法を説明します。

ゲームの概要や操作方法は[README.md](../README.md)、ブランチ・コミット・Pull Requestの運用ルールは[CONTRIBUTING.md](../CONTRIBUTING.md)を参照してください。

## 1. 開発環境

- ゲームエンジン：Unity
- 開発言語：C#
- Unity Editor：`6000.6.8f1`
- 入力処理：Unity Input System `1.20.1`
- レンダリング：Universal Render Pipeline `17.6.0`
- 自動テスト：Unity Test Framework `1.8.0`

Unity Editorのバージョンは`ProjectSettings/ProjectVersion.txt`、パッケージのバージョンは`Packages/manifest.json`を参照してください。

## 2. プロジェクトのセットアップ

1. リポジトリをローカル環境にクローンします。

   ```bash
   git clone https://github.com/FFlushhh/yokero-taiyakikun.git
   ```

2. Unity Hubを起動し、クローンしたプロジェクトのルートフォルダを追加します。
3. `ProjectSettings/ProjectVersion.txt`に記載されたバージョンのUnity Editorでプロジェクトを開きます。
4. Unityによるアセットのインポートとパッケージの読み込みが完了したことを確認します。
5. Unity EditorのConsoleにコンパイルエラーが発生していないことを確認します。

プロジェクトが使用するパッケージは`Packages/manifest.json`で管理されています。依存関係の詳細は`Packages/packages-lock.json`を参照してください。

## 3. ゲームの実行

Unity Editorで`Assets/Scenes/TitleScene.unity`を開き、Playボタンから実行します。

主なSceneは以下の3つです。

| Scene | 役割 |
|---|---|
| `TitleScene.unity` | タイトル画面、ゲーム開始 |
| `MainScene.unity` | プレイヤー操作、敵の生成、ゲーム進行 |
| `ResultScene.unity` | 生存時間・ランキングの表示、リトライ |

各Sceneは`Assets/Scenes/`に配置されています。

ビルド対象のSceneとその順序は`ProjectSettings/EditorBuildSettings.asset`で管理されています。

なお、`MainScene_old.unity`もリポジトリ内に存在しますが、現在のビルド設定では無効になっています。

## 4. 自動テスト

本プロジェクトではUnity Test Frameworkを使用し、Edit ModeおよびPlay Modeのテストを実装しています。

テストコードは`Assets/Tests/`に配置されています。

### 4.1 主なテスト対象

- プレイヤーの高速移動時の衝突判定
- プレイヤーの移動方向・回転制御
- プレイヤーとMainSceneの設定
- ゲームオーバー理由の管理
- タイトル画面のオブジェクト設定
- タイトルロゴのインタラクション
- 泡の破壊処理と隠し演出
- タイトル画面の各種エフェクト

### 4.2 テストの実行方法

1. Unity Editorを開きます。
2. メニューから`Window > General > Test Runner`を選択します。
3. `EditMode`タブで対象のテストを選択し、実行します。
4. `PlayMode`タブでも対象のテストを選択し、実行します。
5. テスト結果を確認します。

テストの実行方法については、[Unity公式ドキュメント](https://docs.unity3d.com/ja/current/Manual/testing-editortestsrunner.html)も参照してください。

### 4.3 手動での動作確認

画面表示や操作感、演出などについては、必要に応じてUnity Editor上で手動確認を行います。

主な確認対象は以下のとおりです。

- タイトル画面からゲーム画面への遷移
- マウスによるプレイヤー操作
- 敵との接触によるゲーム終了
- 生存時間とランキングの表示
- リトライおよびタイトル画面への復帰
- タイトル画面のインタラクションと各種演出

## 5. 主なディレクトリ構成

| ディレクトリ | 内容 |
|---|---|
| `Assets/Scenes/` | ゲームのScene |
| `Assets/Scripts/` | ゲームシステムや操作処理のC#スクリプト |
| `Assets/Scripts/Collision/` | 衝突判定関連の処理 |
| `Assets/Scripts/TitleBackground/` | タイトル画面の背景・インタラクション |
| `Assets/Tests/` | Unity Test Frameworkによる自動テスト |
| `Assets/Art/` | キャラクターやタイトル背景などの画像 |
| `Assets/Animations/` | アニメーション関連のファイル |
| `Assets/Prefabs/` | 再利用するGameObject |
| `Assets/AudioSources/` | BGM・効果音 |
| `Packages/` | Unityパッケージの依存関係 |
| `ProjectSettings/` | Unityプロジェクトの設定 |

## 6. 開発時の注意事項

- Unity Editorは原則として`ProjectSettings/ProjectVersion.txt`に記載されたバージョンを使用してください。
- Unityのアセットと対応する`.meta`ファイルを適切に管理してください。
- `Assets`、`Packages`、`ProjectSettings`の変更をコミットする際は、意図しない差分が含まれていないことを確認してください。
- スクリプトやSceneを変更した場合は、関連する自動テストと手動テストを実施してください。
- ブランチ、コミット、Pull Requestの運用については[CONTRIBUTING.md](../CONTRIBUTING.md)に従ってください。