<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Completa o que o Beautifier do PL/SQL Developer não faz, transforma uma consulta de um pacote em uma que roda sozinha e elimina pequenas tarefas de texto. Plug-in nativo de 64 bits: só uma DLL.</p>

<p align="center">[English](../../README.md) · [Русский](README.ru.md) · [Oʻzbekcha](README.uz.md) · [Español](README.es.md) · **Português** · [中文](README.zh.md) · [日本語](README.ja.md) · [हिन्दी](README.hi.md)</p>

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

## Que problemas resolve

### 1. O Beautifier não segue o seu padrão de código

`END nome;`, `:=` em colunas, o layout de `INSERT` / `SELECT` / `JOIN`, linhas longas, linhas em branco. O **Format Tools** adiciona essas regras depois do Beautifier. Uma caixa por regra, pré-visualização ao vivo, e as regras podem ser exportadas para um arquivo e compartilhadas com a equipe.

<p align="center"><img src="../assets/format.png" alt="Before and after Format Tools" width="880"></p>
<p align="center"><img src="../assets/settings.png" alt="Format Tools settings window" width="640"></p>

### 2. Uma consulta com constantes de pacote não executa

**Copy SQL with constants resolved**: `c_new` vira `'NEW'`, variáveis viram binds, o `INTO` é removido. Cole numa SQL Window ou no Explain Plan.

<p align="center"><img src="../assets/copy-sql.png" alt="Copy SQL with constants resolved" width="880"></p>

### 3. ORA-01795: mais de 1000 valores no `IN`

Cole uma lista (linhas ou `1, 2, 3`) e receba `IN (...)` dividido em grupos que o Oracle aceita.

<p align="center"><img src="../assets/in-list.png" alt="List to IN (...)" width="420"></p>

### 4. Tarefas pequenas, um clique

camelCase / snake_case, aspas e `q'[...]'`, Base64 / HEX / URL, ordenar linhas, a data atual como `TO_DATE`, epoch e máscaras de data Java/.NET. Todos os comandos: [commands.md](../commands.md).

> O código em si não muda: os tokens são comparados antes e depois, e uma regra que mexesse em mais que o layout é ignorada.

## Instalação

1. Baixe `PlsqlDevTextTools.dll` da [última versão](https://github.com/kivilev/plsql-developer-text-tools/releases/latest).
2. Feche o PL/SQL Developer, copie a DLL para `<PL/SQL Developer>\PlugIns` (precisa de administrador) e abra a IDE.
3. Se o menu não aparecer: **Configure → Plug-Ins**, ative *Text Tools* e reinicie.

PL/SQL Developer 12–17 x64, cada versão é testada no CI. Sem .NET, sem acesso à rede.
Atalhos: **Configure → Preferences → User Interface → Key Configuration**, filtre por `Text Tools`.

---

Dúvidas: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · Bugs: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · Código: [CONTRIBUTING](../../CONTRIBUTING.md) · Mudanças: [CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**: desenvolvedor backend desde 2005, Oracle Certified Professional, fundador da escola [Backend PRO](https://backend-pro.ru).

**Licença:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer é marca da Allround Automations; Oracle e PL/SQL são marcas da Oracle Corporation. O projeto não é afiliado a nenhuma das duas.</sub>
