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

### 1. Расширенный Beautifier

*Format Tools*

Работает после Beautifier в IDE и добавляет то, что его файл правил выразить не может: больше 25 опций, каждая отдельной галочкой, с превью. Правила можно выгрузить в файл для команды (совпадающие настройки из `.br` файла Beautifier можно импортировать).

- `END имя;`, `:=` в столбик, выровненные объявления, комментарии с отступом своего кода
- раскладка `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON`, подзапросов
- длинные строки переносятся по правому полю: `DECODE` парами, `BETWEEN`, `||`, `=>` в столбик
- условия `IF` одной логической строкой, короткие группы в скобках в одну строку
- встроенные функции и `%TYPE` в верхнем регистре, пробел после запятой (по желанию)
- пустые строки между подпрограммами и без пустой перед закрывающим `END`, раскладка `EXECUTE IMMEDIATE`, без пробелов в хвостах
- сам код не меняется: токены сравниваются до и после, правило, которое трогает больше чем оформление, пропускается

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. Копирование запроса с подстановкой констант

*SQL Tools*

Поставьте курсор в запрос внутри пакета, процедуры или блока и получите оператор, который запускается сам в SQL Window или Explain Plan.

- константы PL/SQL заменяются значениями: `status = c_new` → `status = 'NEW' /*c_new*/`
- переменные и параметры становятся bind-переменными (`:i_customer`), `INTO`, `BULK COLLECT INTO` и `RETURNING ... INTO` убираются
- константы ищутся как в PL/SQL: блок, тело пакета, спецификация; чужие пакеты читаются из базы, если есть соединение
- **Explain Plan with constants**: тот же оператор в окне плана IDE

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. Строковые утилиты

*String Tools*

Всё, что вы перепечатываете руками, одним щелчком: на выделении или на всём тексте.

- **Список → `IN (...)`** из строк, `1 2 3`, `a, b` или Excel; больше 1000 значений не упираются в ORA-01795
- **Регистр**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, кириллица)
- **Кавычки**: экранирование и обратно, `q'[...]'`, строки → `'a' || chr(10) || 'b'`
- **Кодирование**: Base64, URL, HEX
- **Строки**: сортировка, разворот, удаление дублей и пустых строк, обрезка хвостов, выравнивание по `:=` и `=>`

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. Утилиты для дат

*Date Tools*

Текущее время и все форматы дат, которые приходится постоянно гуглить.

- **Сейчас**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (из `SYSTIMESTAMP`, если есть соединение)
- **Конвертация**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` или литерал `DATE` / `TIMESTAMP`
- **Маски**: Java и .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

Все команды с примерами: [commands.md](../commands.md)

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
