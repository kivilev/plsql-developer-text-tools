<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Доделывает то, что не умеет Beautifier в PL/SQL Developer, превращает запрос из пакета в рабочий и убирает мелкую текстовую рутину. Нативный 64-битный плагин: ставится одной DLL.</p>

<p align="center"><a href="../../README.md">English</a> &middot; <b>Русский</b> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <a href="README.es.md">Español</a> &middot; <a href="README.pt.md">Português</a> &middot; <a href="README.zh.md">中文</a> &middot; <a href="README.ja.md">日本語</a> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

## Какие боли решает

### 1. Beautifier не соблюдает стандарт кода

`END имя;`, `:=` в столбик, раскладка `INSERT` / `SELECT` / `JOIN`, длинные строки, пустые строки. **Format Tools** добавляет эти правила после Beautifier. Каждое правило отдельной галочкой, превью сразу видно, правила можно выгрузить в файл и раздать команде.

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. Запрос с константами пакета не запускается

**Copy SQL with constants resolved**: `c_new` становится `'NEW'`, переменные — bind-переменными, `INTO` убирается. Вставляйте в SQL Window или Explain Plan.

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795: больше 1000 значений в `IN`

Вставьте список (строки или `1, 2, 3`) и получите `IN (...)`, разбитый на группы, которые Oracle принимает.

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. Мелкая рутина — одним щелчком

camelCase / snake_case, кавычки и `q'[...]'`, Base64 / HEX / URL, сортировка строк, текущая дата как `TO_DATE`, epoch и маски дат Java/.NET. Все команды: [commands.md](../commands.md).

> Сам код не меняется: токены сравниваются до и после, а правило, которое тронуло бы больше, чем оформление, пропускается.

## Установка

1. Скачайте `PlsqlDevTextTools.dll` из [последнего релиза](https://github.com/kivilev/plsql-developer-text-tools/releases/latest).
2. Закройте PL/SQL Developer, скопируйте DLL в `<PL/SQL Developer>\PlugIns` (нужны права администратора), запустите IDE.
3. Если меню нет: **Configure → Plug-Ins**, включите *Text Tools*, перезапустите.

PL/SQL Developer 12–17 x64, в CI проверяется каждая версия. .NET не нужен, сеть не используется.
Горячие клавиши: **Configure → Preferences → User Interface → Key Configuration**, фильтр `Text Tools`.

---

Вопросы: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · Баги: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · Код: [CONTRIBUTING](../../CONTRIBUTING.md) · Изменения: [CHANGELOG](../../CHANGELOG.md)

**Денис Кивилев**: backend-разработчик с 2005 года, Oracle Certified Professional, основатель школы [Backend PRO](https://backend-pro.ru).

**Лицензия:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer — торговая марка Allround Automations; Oracle и PL/SQL — торговые марки Oracle Corporation. Проект не связан ни с одной из компаний.</sub>
