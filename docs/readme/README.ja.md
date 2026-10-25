<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">PL/SQL Developer の Beautifier では足りない整形を補い、パッケージ内のクエリをそのまま実行できる形にし、細かなテキスト作業をなくします。ネイティブ 64 ビットのプラグインで、DLL ひとつだけです。</p>

<p align="center">[English](../../README.md) · [Русский](README.ru.md) · [Oʻzbekcha](README.uz.md) · [Español](README.es.md) · [Português](README.pt.md) · [中文](README.zh.md) · **日本語** · [हिन्दी](README.hi.md)</p>

<p align="center">
  <a href="https://github.com/kivilev/plsql-developer-text-tools/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/kivilev/plsql-developer-text-tools/actions/workflows/ci.yml/badge.svg?branch=master"></a>
  <a href="https://github.com/kivilev/plsql-developer-text-tools/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/kivilev/plsql-developer-text-tools?sort=semver"></a>
  <a href="https://github.com/kivilev/plsql-developer-text-tools/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/kivilev/plsql-developer-text-tools/total"></a>
  <a href="../../LICENSE"><img alt="License: MIT" src="https://img.shields.io/github/license/kivilev/plsql-developer-text-tools"></a>
  <br>
  <img alt="Windows x64" src="https://img.shields.io/badge/platform-Windows%20x64-0078D6?logo=windows">
  <img alt="PL/SQL Developer 12+ x64" src="https://img.shields.io/badge/PL%2FSQL%20Developer-12%2B%20x64-6E7781">
  <img alt=".NET 10 Native AOT" src="https://img.shields.io/badge/.NET%2010-Native%20AOT-512BD4?logo=dotnet">
</p>

<p align="center"><img src="../assets/demo.gif" alt="Demo" width="880"></p>

## 解決する課題

### 1. Beautifier がコード規約に従わない

`END 名前;`、`:=` の桁揃え、`INSERT` / `SELECT` / `JOIN` のレイアウト、長い行、空行。**Format Tools** が Beautifier の後にこれらのルールを加えます。ルールごとにチェックボックス、リアルタイムのプレビュー、ルールはファイルに書き出してチームで共有できます。

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. パッケージの定数を含むクエリが実行できない

**Copy SQL with constants resolved**：`c_new` は `'NEW'` に、変数はバインド変数に、`INTO` は削除されます。SQL Window や Explain Plan にそのまま貼り付けられます。

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795：`IN` に 1000 件超

リスト（改行区切りまたは `1, 2, 3`）を貼ると、Oracle が受け付けるグループに分けた `IN (...)` になります。

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. 細かな作業をワンクリックで

camelCase / snake_case、引用符と `q'[...]'`、Base64 / HEX / URL、行の並べ替え、現在日時の `TO_DATE`、epoch と Java/.NET の日付書式。全コマンド：[commands.md](../commands.md)。

> コード自体は変わりません。整形の前後でトークンを比較し、レイアウト以外に触れるルールはスキップします。

## インストール

1. [最新リリース](https://github.com/kivilev/plsql-developer-text-tools/releases/latest)から `PlsqlDevTextTools.dll` をダウンロードします。
2. PL/SQL Developer を閉じ、DLL を `<PL/SQL Developer>\PlugIns` にコピー（管理者権限が必要）して IDE を起動します。
3. メニューが出ない場合：**Configure → Plug-Ins** で *Text Tools* を有効にして再起動します。

PL/SQL Developer 12–17 x64。CI で全バージョンをテストしています。.NET は不要で、ネットワークも使いません。
ホットキー：**Configure → Preferences → User Interface → Key Configuration** で `Text Tools` を検索します。

---

質問：[Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · 不具合：[issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · コード：[CONTRIBUTING](../../CONTRIBUTING.md) · 変更履歴：[CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**：2005 年からバックエンド開発者、Oracle Certified Professional、[Backend PRO](https://backend-pro.ru) スクール創設者。

**ライセンス:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer は Allround Automations の商標、Oracle と PL/SQL は Oracle Corporation の商標です。本プロジェクトはどちらとも関係ありません。</sub>
