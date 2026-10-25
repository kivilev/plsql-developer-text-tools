<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Completa lo que el Beautifier de PL/SQL Developer no hace, convierte una consulta de un paquete en una que se ejecuta sola y elimina las pequeñas tareas de texto. Plug-in nativo de 64 bits: solo una DLL.</p>

<p align="center">[English](../../README.md) · [Русский](README.ru.md) · [Oʻzbekcha](README.uz.md) · **Español** · [Português](README.pt.md) · [中文](README.zh.md) · [日本語](README.ja.md) · [हिन्दी](README.hi.md)</p>

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

### 1. El Beautifier no sigue tu estándar de código

`END nombre;`, `:=` en columnas, el formato de `INSERT` / `SELECT` / `JOIN`, líneas largas, líneas en blanco. **Format Tools** añade estas reglas después del Beautifier. Una casilla por regla, vista previa en vivo, y las reglas se exportan a un archivo para compartirlas con el equipo.

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. Una consulta con constantes de paquete no se ejecuta

**Copy SQL with constants resolved**: `c_new` pasa a `'NEW'`, las variables pasan a binds, se quita `INTO`. Pégala en una SQL Window o en Explain Plan.

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795: más de 1000 valores en `IN`

Pega una lista (líneas o `1, 2, 3`) y obtén `IN (...)` dividido en grupos que Oracle acepta.

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. Tareas pequeñas, un clic

camelCase / snake_case, comillas y `q'[...]'`, Base64 / HEX / URL, ordenar líneas, la fecha actual como `TO_DATE`, epoch y máscaras de fecha Java/.NET. Todos los comandos: [commands.md](../commands.md).

> El código en sí no cambia: los tokens se comparan antes y después, y una regla que tocara algo más que el formato se omite.

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
