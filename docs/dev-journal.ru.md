# Development journal

*In Russian. Newest entries first: date — what was done / learned / decided.*


## 2026-10-14 — отдельный репозиторий

- Выделен в `kivilev/plsql-developer-text-tools` (приватный). Структура: TextTools.slnx, Directory.Build.props,
  Directory.Packages.props (центральные версии), global.json.
- Анализаторы как ошибки: .NET (latest-recommended + code style), Meziantou.Analyzer, SonarAnalyzer.CSharp.
  Нашли по делу: регулярки без таймаута (MA0009) — добавлен Limits.RegexTimeoutMs и обработка таймаута в статус-бар.
- Тесты: ядро 125 (покрытие 97.7% строк, порог 95%, без кода генератора регулярок), меню 6;
  эмулятор IDE стал интеграционным тестом с кодом возврата (tools/HostEmulator, режим all).
- scripts/verify.ps1 = все проверки, тот же скрипт в CI (windows-latest). Плюс commit-rules и gitleaks.
- Правила коммитов по образцу cbs-db: feat|fix|opt[#issue] comment; вместо Jira — GitHub issues (обязателен для fix).
- Скилы в .claude/skills: plsqldev-plugin-api, add-command, verify, install-to-ide, fix-bug, git-workflow, release.

## 2026-10-14 — надёжность: плагин не должен ронять IDE

- Требование пользователя: никаких падений IDE, утечек, порчи текста. Аудит и меры — в CLAUDE.md («Правила»).
- Фаззинг всех 33 преобразований (≈160 злых входов + 300 случайных на команду) нашёл 3 дыры, которые дали бы
  диалог ошибки: смещение +99:99, epoch long.MinValue, P99999999999999999999Y (переполнение). Исправлено.
- Закрепление DLL (GetModuleHandleEx PIN): эмулятор делает FreeLibrary ×3 + 2 с давления GC — процесс жив.
- Утечек нет: 150 тыс. случайных кликов, private bytes 25.5 → 24.7 МБ (плато).
- Без колбэков IDE и при отказе UTF-8 — без падений; текст с кириллицей без UTF-8 не трогаем.
- Статус-бар: после команды — фиксированный пример «вход -> результат» (вычисляется самой командой).
- Открытый вопрос: без выделения команда применяется ко всему тексту (Sort по всему скрипту!) — до проверки undo
  это риск потери данных.

## 2026-10-14 — Date Tools, переименование в Text Tools

- Продукт переименован: «Text Tools for PL/SQL Developer»; проекты TextTools.*, DLL `TextTools.dll` (старый StringTools.dll
  из PlugIns удалён). Записи журнала ниже после sed тоже говорят TextTools — до этой даты продукт назывался String Tools.
- Меню: Plug-Ins → String Tools ▸ (25) и Date Tools ▸ (15). Модель команд: TransformCommand / InsertCommand.
- Date Tools: Now (7 форматов; SYSTIMESTAMP из основной сессии IDE, без подключения — часы ПК, источник пишется в статус),
  Convert (epoch с/мс/мкс/нс → TIMESTAMP, дата → epoch / TO_DATE|TO_TIMESTAMP|TO_TIMESTAMP_TZ / литерал; построчно),
  Masks (Java/.NET ↔ Oracle), ISO duration → INTERVAL (переносы, точность, отказ при смешении Y/M и D/T).
- Неоднозначные даты (01/02/2026) не трогаем — сообщение в статусе. Ошибки ввода — в статус, не диалогом.
- Native AOT с UseSystemResourceKeys вырезает тексты системных исключений («Format_BadBase64Char») → свои сообщения.
- Тесты: 88/88. Эмулятор хоста: все сценарии ок. Установлено в IDE 14. В живой IDE Date Tools ещё не проверен,
  как и undo после SetText.

## 2026-10-13 (вечер) — первая сборка

- Native AOT собрался: `TextTools.dll` 1,4 МБ, x64, 7 экспортов (dumpbin). Нюанс: ILCompiler ищет `vswhere.exe`
  в PATH — добавить `C:\Program Files (x86)\Microsoft Visual Studio\Installer`.
- Хост-эмулятор (грузит DLL, подставляет фейковые колбэки, жмёт меню): меню из 30 пунктов, CHARMODE=UTF8 ставится,
  кириллица и замена выделения работают, курсор восстанавливается.
- Подписи меню сделали ASCII-only (IDE может прочитать меню до OnActivate, т.е. ещё в ANSI) + тест на это.
- Установлен в `C:\Program Files\PLSQL Developer 14\PlugIns\` (нужен админ). Ручная проверка в IDE — за пользователем.

## 2026-10-13

- Старт. Разобрали страницу плагинов Allround: ~30 штук, сторонние почти все 32-бит и 2003–2012 годов;
  в PL/SQL Developer 16 (только x64) реально работают единицы (rrProject, Dependency Viewer, utPLSQL,
  Replace param, Query Notifier, LuaPlSql, Text Export). Ниша свободна.
- Уже встроено в IDE (не делаем): AI Assistant, Git/SVN, Beautifier, Upper/Lower в Edit > Selection, шаблоны.
- API: DLL с экспортами `IdentifyPlugIn`, `RegisterCallback`, `CreateMenuItem`, `OnMenuClick` и др.
  Рисовать в редакторе нельзя (без подчёркиваний/inline), только текст, меню, SQL, экспорт гридов.
  `IDE_SetText` заменяет весь текст → выделение заменяем через поиск позиции у курсора.
- Решение: C# + .NET 10 Native AOT (один нативный DLL, без CLR в процессе IDE).
- Каркас v0: Case (camel/Pascal/snake/UPPER_SNAKE/kebab), SQL (escape, q-quote, IN-список с обходом
  лимита 1000, конкатенация строк), Encode (Base64/URL/HEX), Lines (сортировки, дубли, trim, выравнивание `:=`/`=>`).
- Бэклог: CSV/Excel из буфера → INSERT/`WITH ... UNION ALL`; экспорт грида в Markdown/Jira (`RegisterExport`);
  JSON pretty/minify и генерация `JSON_TABLE`; Todo по `ALL_SOURCE`; иконки кнопок.
- Проверить руками в IDE: попадает ли `IDE_SetText` в undo; `GetCursorX` — символ или визуальная колонка при табах.
