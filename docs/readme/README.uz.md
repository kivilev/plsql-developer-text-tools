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

### 1. Kengaytirilgan Beautifier

*Format Tools*

IDE Beautifier'idan keyin ishlaydi va uning qoidalar fayli ifodalay olmaydigan narsalarni qo‘shadi: 25 dan ortiq variant, har biri alohida belgi, natija darhol ko‘rinadi. Qoidalarni jamoa uchun faylga saqlash mumkin (Beautifier `.br` faylidagi mos sozlamalarni import qilish mumkin).

- `END nom;`, `:=` ustun bo‘yicha, tekislangan e’lonlar, izohlar o‘z kodi chekinishida
- `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON`, ichki so‘rovlar joylashuvi
- uzun qatorlar o‘ng chegarada bo‘linadi: `DECODE` juftlab, `BETWEEN`, `||`, `=>` ustun bo‘yicha
- `IF` shartlari bitta mantiqiy qatorda, qavs ichidagi qisqa guruhlar bir qatorda
- o‘rnatilgan funksiyalar va `%TYPE` katta harfda, xohlasangiz vergul keyin bo‘sh joy
- protseduralar orasida bo‘sh qator, yopuvchi `END` oldida yo‘q, `EXECUTE IMMEDIATE` joylashuvi, oxirgi bo‘sh joylarsiz
- kodning o‘zi o‘zgarmaydi: tokenlar oldin va keyin solishtiriladi, ko‘rinishdan ko‘pini o‘zgartiradigan qoida o‘tkazib yuboriladi

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. So‘rovni konstantalar bilan nusxalash

*SQL Tools*

Kursorni paket, protsedura yoki blok ichidagi so‘rovga qo‘ying va SQL Window yoki Explain Plan'da o‘zi ishlaydigan operatorni oling.

- PL/SQL konstantalari qiymatga almashadi: `status = c_new` → `status = 'NEW' /*c_new*/`
- o‘zgaruvchi va parametrlar bind-o‘zgaruvchiga aylanadi (`:i_customer`), `INTO`, `BULK COLLECT INTO` va `RETURNING ... INTO` olib tashlanadi
- konstantalar PL/SQL kabi qidiriladi: blok, paket tanasi, spetsifikatsiya; ulanish bo‘lsa boshqa paketlar bazadan o‘qiladi
- **Explain Plan with constants**: o‘sha operator IDE reja oynasida

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. Satr utilitalari

*String Tools*

Qo‘lda qayta yozadigan hamma narsa bir bosishda: belgilangan matnda yoki butun matnda.

- **Ro‘yxat → `IN (...)`** qatorlar, `1 2 3`, `a, b` yoki Excel'dan; 1000 dan ortiq qiymat ORA-01795 ga olib kelmaydi
- **Registr**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, kirill)
- **Qo‘shtirnoq**: ekranlash va qaytarish, `q'[...]'`, qatorlar → `'a' || chr(10) || 'b'`
- **Kodlash**: Base64, URL, HEX
- **Qatorlar**: saralash, teskari tartib, takrorlar va bo‘sh qatorlarni o‘chirish, oxirgi bo‘sh joylarni kesish, `:=` va `=>` bo‘yicha tekislash

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. Sana utilitalari

*Date Tools*

Joriy vaqt va doim qidiradigan barcha sana formatlari.

- **Hozir**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (ulanish bo‘lsa `SYSTIMESTAMP` dan)
- **Aylantirish**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` yoki `DATE` / `TIMESTAMP` literali
- **Niqoblar**: Java va .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

Misollar bilan barcha buyruqlar: [commands.md](../commands.md)

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
