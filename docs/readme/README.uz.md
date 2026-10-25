<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">PL/SQL Developer Beautifier bajarolmaydigan ishni tugatadi, paketdagi so‘rovni mustaqil ishlaydigan holatga keltiradi va mayda matn ishlarini yo‘qotadi. Mahalliy 64-bitli plagin: bitta DLL yetarli.</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <b>Oʻzbekcha</b> &middot; <a href="README.es.md">Español</a> &middot; <a href="README.pt.md">Português</a> &middot; <a href="README.zh.md">中文</a> &middot; <a href="README.ja.md">日本語</a> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

## Qanday muammolarni hal qiladi

### 1. Beautifier kod standartiga rioya qilmaydi

`END nom;`, `:=` ustun bo‘yicha, `INSERT` / `SELECT` / `JOIN` joylashuvi, uzun qatorlar, bo‘sh qatorlar. **Format Tools** bu qoidalarni Beautifier'dan keyin qo‘shadi. Har bir qoida alohida belgi, natija darhol ko‘rinadi, qoidalarni faylga saqlab, jamoaga berish mumkin.

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. Paket konstantalari bor so‘rov ishlamaydi

**Copy SQL with constants resolved**: `c_new` `'NEW'` bo‘ladi, o‘zgaruvchilar bind-o‘zgaruvchiga aylanadi, `INTO` olib tashlanadi. SQL Window yoki Explain Plan'ga qo‘ying.

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795: `IN` da 1000 dan ortiq qiymat

Ro‘yxatni qo‘ying (qatorlar yoki `1, 2, 3`) — Oracle qabul qiladigan guruhlarga bo‘lingan `IN (...)` oling.

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. Mayda ishlar — bir bosishda

camelCase / snake_case, qo‘shtirnoqlar va `q'[...]'`, Base64 / HEX / URL, qatorlarni saralash, joriy sana `TO_DATE` sifatida, epoch va Java/.NET sana niqoblari. Barcha buyruqlar: [commands.md](../commands.md).

> Kodning o‘zi o‘zgarmaydi: tokenlar oldin va keyin solishtiriladi, faqat ko‘rinishdan ko‘pini o‘zgartiradigan qoida o‘tkazib yuboriladi.

## O‘rnatish

1. `PlsqlDevTextTools.dll` ni [so‘nggi reliz](https://github.com/kivilev/plsql-developer-text-tools/releases/latest)dan yuklab oling.
2. PL/SQL Developer'ni yoping, DLL'ni `<PL/SQL Developer>\PlugIns` papkasiga nusxalang (administrator huquqi kerak), IDE'ni ishga tushiring.
3. Menyu yo‘q bo‘lsa: **Configure → Plug-Ins**, *Text Tools* ni yoqing, qayta ishga tushiring.

PL/SQL Developer 12–17 x64, CI da har bir versiya tekshiriladi. .NET kerak emas, tarmoq ishlatilmaydi.
Tezkor tugmalar: **Configure → Preferences → User Interface → Key Configuration**, filtr `Text Tools`.

---

Savollar: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · Xatolar: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · Kod: [CONTRIBUTING](../../CONTRIBUTING.md) · O‘zgarishlar: [CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**: 2005-yildan beri backend dasturchi, Oracle Certified Professional, [Backend PRO](https://backend-pro.ru) maktabi asoschisi.

**Litsenziya:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer — Allround Automations savdo belgisi; Oracle va PL/SQL — Oracle Corporation savdo belgilari. Loyiha ularning hech biriga aloqador emas.</sub>
