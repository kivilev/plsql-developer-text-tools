<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">补足 PL/SQL Developer Beautifier 做不到的格式化，让包中的查询可以直接运行，并省去琐碎的文本操作。原生 64 位插件，只需一个 DLL。</p>

<p align="center">[English](../../README.md) · [Русский](README.ru.md) · [Oʻzbekcha](README.uz.md) · [Español](README.es.md) · [Português](README.pt.md) · **中文** · [日本語](README.ja.md) · [हिन्दी](README.hi.md)</p>

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

## 解决的痛点

### 1. Beautifier 不符合你的代码规范

`END 名称;`、`:=` 按列对齐、`INSERT` / `SELECT` / `JOIN` 排版、过长的行、空行。**Format Tools** 在 Beautifier 之后补上这些规则。每条规则一个复选框，实时预览，规则可导出为文件分享给团队。

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. 带包常量的查询无法直接运行

**Copy SQL with constants resolved**：`c_new` 变成 `'NEW'`，变量变成绑定变量，去掉 `INTO`。直接粘贴到 SQL Window 或 Explain Plan。

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795：`IN` 中超过 1000 个值

粘贴列表（每行一个或 `1, 2, 3`），得到按 Oracle 可接受的分组拆开的 `IN (...)`。

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. 琐碎工作，一键完成

camelCase / snake_case、引号与 `q'[...]'`、Base64 / HEX / URL、行排序、当前日期转 `TO_DATE`、epoch 与 Java/.NET 日期格式。全部命令：[commands.md](../commands.md)。

> 代码本身不会被改动：格式化前后会比较所有记号，超出排版范围的规则会被跳过。

## 安装

1. 从[最新发布](https://github.com/kivilev/plsql-developer-text-tools/releases/latest)下载 `PlsqlDevTextTools.dll`。
2. 关闭 PL/SQL Developer，把 DLL 复制到 `<PL/SQL Developer>\PlugIns`（需要管理员权限），再启动 IDE。
3. 没有菜单时：**Configure → Plug-Ins**，启用 *Text Tools*，重启。

PL/SQL Developer 12–17 x64，每个版本都在 CI 中测试。无需 .NET，不使用网络。
快捷键：**Configure → Preferences → User Interface → Key Configuration**，筛选 `Text Tools`。

---

提问：[Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · 缺陷：[issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · 代码：[CONTRIBUTING](../../CONTRIBUTING.md) · 变更：[CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**：2005 年起从事后端开发，Oracle Certified Professional，[Backend PRO](https://backend-pro.ru) 学校创始人。

**许可证:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer 是 Allround Automations 的商标；Oracle 和 PL/SQL 是 Oracle Corporation 的商标。本项目与两家公司均无关联。</sub>
