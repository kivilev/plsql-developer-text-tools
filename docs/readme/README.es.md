<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Completa lo que el Beautifier de PL/SQL Developer no hace, convierte una consulta de un paquete en una que se ejecuta sola y elimina las pequeñas tareas de texto. Plug-in nativo de 64 bits: solo una DLL.</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <b>Español</b> &middot; <a href="README.pt.md">Português</a> &middot; <a href="README.zh.md">中文</a> &middot; <a href="README.ja.md">日本語</a> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

## Qué problemas resuelve

### 1. Beautifier ampliado

*Format Tools*

Se ejecuta después del Beautifier del IDE y añade lo que su archivo de reglas no puede expresar: más de 25 opciones, cada una con su casilla y vista previa en vivo. Las reglas se exportan a un archivo para el equipo (se pueden importar los ajustes coincidentes del `.br` del Beautifier).

- `END nombre;`, `:=` en columnas, declaraciones alineadas, comentarios con la sangría de su código
- formato de `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON` y subconsultas
- líneas largas cortadas en el margen: `DECODE` por pares, `BETWEEN`, `||`, `=>` alineados
- condiciones `IF` en una línea lógica, grupos cortos entre paréntesis en una línea
- funciones integradas y `%TYPE` en mayúsculas, un espacio tras cada coma (opcional)
- líneas en blanco entre subprogramas y ninguna antes del `END` final, formato de `EXECUTE IMMEDIATE`, sin espacios al final
- el código no cambia: los tokens se comparan antes y después y una regla que toque algo más que el formato se omite

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. Copiar una consulta con las constantes resueltas

*SQL Tools*

Pon el cursor en una consulta dentro de un paquete, procedimiento o bloque y obtén una sentencia que se ejecuta sola en una SQL Window o en Explain Plan.

- las constantes PL/SQL pasan a su valor: `status = c_new` → `status = 'NEW' /*c_new*/`
- variables y parámetros pasan a binds (`:i_customer`); `INTO`, `BULK COLLECT INTO` y `RETURNING ... INTO` se quitan
- las constantes se buscan como en PL/SQL: bloque, cuerpo del paquete, especificación; con conexión se leen otros paquetes de la base
- **Explain Plan with constants**: la misma sentencia en la ventana de plan del IDE

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. Utilidades de texto

*String Tools*

Todo lo que reescribes a mano, con un clic, sobre la selección o todo el texto.

- **Lista → `IN (...)`** desde líneas, `1 2 3`, `a, b` o Excel; más de 1000 valores sin ORA-01795
- **Mayúsculas**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, cirílico)
- **Comillas**: escapar y desescapar, `q'[...]'`, líneas → `'a' || chr(10) || 'b'`
- **Codificación**: Base64, URL, HEX
- **Líneas**: ordenar, invertir, quitar duplicadas y vacías, recortar, alinear por `:=` y `=>`

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. Utilidades de fechas

*Date Tools*

La hora actual y todos los formatos de fecha que siempre tienes que buscar.

- **Ahora**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (de `SYSTIMESTAMP` si hay conexión)
- **Convertir**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` o literal `DATE` / `TIMESTAMP`
- **Máscaras**: Java y .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

Todos los comandos con ejemplos: [commands.md](../commands.md)

## Instalación

1. Descarga `PlsqlDevTextTools.dll` de la [última versión](https://github.com/kivilev/plsql-developer-text-tools/releases/latest).
2. Cierra PL/SQL Developer, copia la DLL en `<PL/SQL Developer>\PlugIns` (hacen falta permisos de administrador) y abre el IDE.
3. Si falta el menú: **Configure → Plug-Ins**, activa *Text Tools* y reinicia.

PL/SQL Developer 12–17 x64, cada versión se prueba en CI. No requiere .NET ni usa la red.
Atajos: **Configure → Preferences → User Interface → Key Configuration**, filtra por `Text Tools`.

---

Preguntas: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · Errores: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · Código: [CONTRIBUTING](../../CONTRIBUTING.md) · Cambios: [CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**: desarrollador backend desde 2005, Oracle Certified Professional, fundador de la escuela [Backend PRO](https://backend-pro.ru).

**Licencia:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer es marca de Allround Automations; Oracle y PL/SQL son marcas de Oracle Corporation. El proyecto no está afiliado a ninguna de las dos.</sub>
