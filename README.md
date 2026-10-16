# よけろ！たいやきくん

Unityで開発している2D回避ゲームです。

2025年度の大学祭で展示したゲーム「YOKERO（仮）」をベースに、展示時のアンケート結果を参考にしながら、ゲーム体験の改善と新しいキャラクターへの変更を進めています。

## ゲーム概要

プレイヤーをマウスで操作し、画面内に出現する敵を避けながら生存時間を伸ばすゲームです。

### 現在の基本仕様

- マウス操作によるプレイヤー移動
- 敵との接触によるゲームオーバー
- 生存時間の計測
- タイトル、ゲーム、リザルトの各シーン

## 開発環境

- ゲームエンジン：Unity
- 開発言語：C#
- 対象：2Dゲーム
- 使用Unityバージョン：`ProjectSettings/ProjectVersion.txt`を参照

開発環境の詳細は `docs/DEVELOPMENT.md` を参照してください。

## プロジェクトの起動方法

1. リポジトリをクローンする。
2. Unity Hubでプロジェクトのルートフォルダを追加する。
3. `ProjectSettings/ProjectVersion.txt`に記載されたUnity Editorを使用する。
4. `Assets/Scenes/TitleScene.unity`を開く。
5. Unity EditorのPlayボタンから実行する。

## テスト

Unity Test Frameworkを使用しています。

衝突判定関連のテスト：

`Assets/Tests/PlayerPathCollision2DTests.cs`

Unity EditorのTest Runnerから実行できます。

## 開発状況

- T01：高速移動時のすり抜け対策 — 完了
- T02：プレイヤーの当たり判定調整 — キャラクター素材導入後に対応
- T04：ゲームタイトル変更 — 未着手
- T05：たい焼きキャラクター導入 — 未着手

その他の開発タスクはGitHub Issuesで管理します。

## 開発規約

ブランチ・コミット・Pull Requestの運用ルールは `CONTRIBUTING.md` を参照してください。

## 使用アセットとライセンス

使用する外部アセットの著作権および利用条件は、それぞれの権利者に帰属します。

使用素材の一覧やライセンス情報は `docs/ASSET_CREDITS.md` に整理します。

プロジェクト全体の公開・再配布条件は、素材ごとの利用条件を確認したうえで決定します。