<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">PL/SQL Developer の Beautifier では足りない整形を補い、パッケージ内のクエリをそのまま実行できる形にし、細かなテキスト作業をなくします。ネイティブ 64 ビットのプラグインで、DLL ひとつだけです。</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <a href="README.es.md">Español</a> &middot; <a href="README.pt.md">Português</a> &middot; <a href="README.zh.md">中文</a> &middot; <b>日本語</b> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

### 1. 拡張 Beautifier

*Format Tools*

IDE の Beautifier の後に実行され、ルールファイルでは表現できない整形を加えます。25 以上のオプションがそれぞれチェックボックスで、リアルタイムにプレビューできます。ルールはファイルに書き出してチームで共有でき、Beautifier の `.br` ファイルのうち共通する設定は取り込めます。

- `END 名前;`、`:=` の桁揃え、宣言の整列、コメントのインデントをコードに合わせる
- `INSERT`、`SELECT`、`SELECT ... INTO`、`UPDATE ... SET`、`JOIN ... ON`、サブクエリのレイアウト
- 長い行を右マージンで折り返し：`DECODE` は組ごと、`BETWEEN`、`||`、`=>` を揃える
- `IF` の条件を 1 つの論理行に、短い括弧グループは 1 行に
- 組み込み関数と `%TYPE` を大文字に、カンマの後の空白（任意）
- サブプログラム間に空行、閉じる `END` の前は空行なし、`EXECUTE IMMEDIATE` のレイアウト、行末の空白除去
- コード自体は変わりません。前後でトークンを比較し、レイアウト以外に触れるルールはスキップします

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. 定数を解決したクエリのコピー

*SQL Tools*

パッケージ、プロシージャ、ブロック内のクエリにカーソルを置くだけで、SQL Window や Explain Plan でそのまま実行できる文が得られます。

- PL/SQL の定数を値に置換：`status = c_new` → `status = 'NEW' /*c_new*/`
- 変数とパラメータはバインド変数（`:i_customer`）に、`INTO`、`BULK COLLECT INTO`、`RETURNING ... INTO` は削除
- 定数は PL/SQL と同じ順に探索：ブロック、パッケージ本体、仕様部。接続時は他のパッケージをデータベースから取得
- **Explain Plan with constants**：同じ文を IDE のプランウィンドウで表示

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. 文字列ユーティリティ

*String Tools*

手で打ち直していることを、選択範囲またはテキスト全体にワンクリックで。

- **リスト → `IN (...)`**：改行、`1 2 3`、`a, b`、Excel から。1000 件を超えても ORA-01795 になりません
- **ケース**：camelCase、PascalCase、snake_case、UPPER_SNAKE_CASE、kebab-case（`$`、`#`、キリル文字対応）
- **引用符**：エスケープと解除、`q'[...]'`、行 → `'a' || chr(10) || 'b'`
- **エンコード**：Base64、URL、HEX
- **行**：並べ替え、反転、重複・空行の削除、行末の空白除去、`:=` と `=>` の桁揃え

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. 日付ユーティリティ

*Date Tools*

現在時刻と、いつも調べ直している日付形式。

- **現在**：`DATE`、`TIMESTAMP`、`TIMESTAMP WITH TIME ZONE`、`TO_DATE(...)`、ISO 8601、Unix epoch（接続時は `SYSTIMESTAMP`）
- **変換**：epoch ↔ `TIMESTAMP`、`13.10.2026 14:30` → `TO_DATE(...)` または `DATE` / `TIMESTAMP` リテラル
- **書式**：Java と .NET の `yyyy-MM-dd HH:mm` ↔ Oracle の `YYYY-MM-DD HH24:MI`、`PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

例つきの全コマンド: [commands.md](../commands.md)

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
