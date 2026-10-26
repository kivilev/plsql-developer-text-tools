<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">补足 PL/SQL Developer Beautifier 做不到的格式化，让包中的查询可以直接运行，并省去琐碎的文本操作。原生 64 位插件，只需一个 DLL。</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <a href="README.es.md">Español</a> &middot; <a href="README.pt.md">Português</a> &middot; <b>中文</b> &middot; <a href="README.ja.md">日本語</a> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

### 1. 增强版 Beautifier

*Format Tools*

在 IDE 的 Beautifier 之后运行，补上其规则文件无法表达的内容：25 个以上选项，每个一个复选框，带实时预览。规则可导出为文件分享给团队（可导入 Beautifier `.br` 文件中相同的设置）。

- `END 名称;`、`:=` 按列对齐、对齐声明、注释与所属代码同缩进
- `INSERT`、`SELECT`、`SELECT ... INTO`、`UPDATE ... SET`、`JOIN ... ON`、子查询的排版
- 过长的行在右边距处换行：`DECODE` 成对、`BETWEEN`、`||`、`=>` 对齐
- `IF` 条件合并为一个逻辑行，括号内的短条件放在一行
- 内置函数和 `%TYPE` 大写，逗号后加空格（可选）
- 子程序之间空行、结尾 `END` 前不空行、`EXECUTE IMMEDIATE` 排版、去掉行尾空格
- 代码本身不变：前后对比所有记号，超出排版范围的规则会被跳过

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. 复制已解析常量的查询

*SQL Tools*

把光标放在包、过程或块中的查询里，得到可以直接在 SQL Window 或 Explain Plan 中运行的语句。

- PL/SQL 常量替换为其值：`status = c_new` → `status = 'NEW' /*c_new*/`
- 变量和参数变为绑定变量（`:i_customer`），去掉 `INTO`、`BULK COLLECT INTO` 和 `RETURNING ... INTO`
- 常量的查找方式与 PL/SQL 一致：块、包体、包规范；已连接时从数据库读取其他包
- **Explain Plan with constants**：同一语句显示在 IDE 的计划窗口

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. 字符串工具

*String Tools*

所有你手工重复输入的内容，一键完成，作用于选中文本或全部文本。

- **列表 → `IN (...)`**：来自多行、`1 2 3`、`a, b` 或 Excel；超过 1000 个值也不会触发 ORA-01795
- **大小写**：camelCase、PascalCase、snake_case、UPPER_SNAKE_CASE、kebab-case（支持 `$`、`#`、西里尔字母）
- **引号**：转义与还原、`q'[...]'`、多行 → `'a' || chr(10) || 'b'`
- **编码**：Base64、URL、HEX
- **行**：排序、反转、去重、去空行、去行尾空格、按 `:=` 和 `=>` 对齐

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. 日期工具

*Date Tools*

当前时间，以及你总要去查的各种日期格式。

- **当前时间**：`DATE`、`TIMESTAMP`、`TIMESTAMP WITH TIME ZONE`、`TO_DATE(...)`、ISO 8601、Unix epoch（已连接时取自 `SYSTIMESTAMP`）
- **转换**：epoch ↔ `TIMESTAMP`，`13.10.2026 14:30` → `TO_DATE(...)` 或 `DATE` / `TIMESTAMP` 字面量
- **格式掩码**：Java 和 .NET 的 `yyyy-MM-dd HH:mm` ↔ Oracle 的 `YYYY-MM-DD HH24:MI`，`PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

全部命令及示例: [commands.md](../commands.md)

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
