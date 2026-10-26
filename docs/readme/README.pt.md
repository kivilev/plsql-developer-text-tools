<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">Completa o que o Beautifier do PL/SQL Developer não faz, transforma uma consulta de um pacote em uma que roda sozinha e elimina pequenas tarefas de texto. Plug-in nativo de 64 bits: só uma DLL.</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <a href="README.es.md">Español</a> &middot; <b>Português</b> &middot; <a href="README.zh.md">中文</a> &middot; <a href="README.ja.md">日本語</a> &middot; <a href="README.hi.md">हिन्दी</a></p>

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

### 1. Beautifier ampliado

*Format Tools*

Roda depois do Beautifier da IDE e acrescenta o que o arquivo de regras dele não consegue expressar: mais de 25 opções, cada uma com sua caixa e pré-visualização ao vivo. As regras podem ser exportadas para um arquivo da equipe (as configurações coincidentes do `.br` do Beautifier podem ser importadas).

- `END nome;`, `:=` em colunas, declarações alinhadas, comentários com o recuo do seu código
- layout de `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON` e subconsultas
- linhas longas quebradas na margem: `DECODE` em pares, `BETWEEN`, `||`, `=>` alinhados
- condições `IF` em uma linha lógica, grupos curtos entre parênteses em uma linha
- funções internas e `%TYPE` em maiúsculas, espaço após a vírgula (opcional)
- linhas em branco entre subprogramas e nenhuma antes do `END` final, layout de `EXECUTE IMMEDIATE`, sem espaços no fim
- o código não muda: os tokens são comparados antes e depois e uma regra que mexesse em mais que o layout é ignorada

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. Copiar uma consulta com as constantes resolvidas

*SQL Tools*

Ponha o cursor numa consulta dentro de um pacote, procedimento ou bloco e receba um comando que roda sozinho numa SQL Window ou no Explain Plan.

- constantes PL/SQL viram seus valores: `status = c_new` → `status = 'NEW' /*c_new*/`
- variáveis e parâmetros viram binds (`:i_customer`); `INTO`, `BULK COLLECT INTO` e `RETURNING ... INTO` são removidos
- as constantes são achadas como no PL/SQL: bloco, corpo do pacote, especificação; com conexão, outros pacotes são lidos do banco
- **Explain Plan with constants**: o mesmo comando na janela de plano da IDE

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. Utilitários de texto

*String Tools*

Tudo o que você redigita à mão, com um clique, na seleção ou no texto todo.

- **Lista → `IN (...)`** de linhas, `1 2 3`, `a, b` ou Excel; mais de 1000 valores sem ORA-01795
- **Caixa**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, cirílico)
- **Aspas**: escapar e desfazer, `q'[...]'`, linhas → `'a' || chr(10) || 'b'`
- **Codificação**: Base64, URL, HEX
- **Linhas**: ordenar, inverter, remover duplicadas e vazias, aparar, alinhar por `:=` e `=>`

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. Utilitários de datas

*Date Tools*

A hora atual e todos os formatos de data que você vive procurando.

- **Agora**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (de `SYSTIMESTAMP` se conectado)
- **Converter**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` ou literal `DATE` / `TIMESTAMP`
- **Máscaras**: Java e .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

Todos os comandos com exemplos: [commands.md](../commands.md)

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
