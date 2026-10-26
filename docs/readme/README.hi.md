<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../assets/logo-dark.svg">
    <img src="../assets/logo.svg" alt="Text Tools for PL/SQL Developer" width="96">
  </picture>
</p>

<h1 align="center">Text Tools for PL/SQL Developer</h1>

<p align="center">PL/SQL Developer के Beautifier से जो नहीं होता उसे पूरा करता है, पैकेज की क्वेरी को सीधे चलने लायक बनाता है और छोटे-मोटे टेक्स्ट काम हटा देता है। नेटिव 64-बिट प्लग-इन: सिर्फ़ एक DLL।</p>

<p align="center"><a href="../../README.md">English</a> &middot; <a href="README.ru.md">Русский</a> &middot; <a href="README.uz.md">Oʻzbekcha</a> &middot; <a href="README.es.md">Español</a> &middot; <a href="README.pt.md">Português</a> &middot; <a href="README.zh.md">中文</a> &middot; <a href="README.ja.md">日本語</a> &middot; <b>हिन्दी</b></p>

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

## किन समस्याओं को हल करता है

### 1. विस्तारित Beautifier

*Format Tools*

IDE के Beautifier के बाद चलता है और वह जोड़ता है जो उसकी नियम-फ़ाइल व्यक्त नहीं कर सकती: 25 से अधिक विकल्प, हर एक चेकबॉक्स के साथ, लाइव प्रीव्यू के साथ। नियमों को टीम के लिए फ़ाइल में एक्सपोर्ट किया जा सकता है (Beautifier की `.br` फ़ाइल की मेल खाती सेटिंग्स इम्पोर्ट की जा सकती हैं)।

- `END नाम;`, कॉलम में `:=`, संरेखित घोषणाएँ, कमेंट अपने कोड की इंडेंट पर
- `INSERT`, `SELECT`, `SELECT ... INTO`, `UPDATE ... SET`, `JOIN ... ON` और सबक्वेरी का लेआउट
- लंबी लाइनें दाएँ मार्जिन पर टूटती हैं: `DECODE` जोड़ों में, `BETWEEN`, `||`, `=>` संरेखित
- `IF` की शर्तें एक तार्किक लाइन में, कोष्ठक के छोटे समूह एक लाइन में
- बिल्ट-इन फ़ंक्शन और `%TYPE` अपर केस में, कॉमा के बाद स्पेस (वैकल्पिक)
- सबप्रोग्राम के बीच खाली लाइन, अंतिम `END` से पहले नहीं, `EXECUTE IMMEDIATE` का लेआउट, अंत के स्पेस हटाना
- कोड खुद नहीं बदलता: पहले और बाद टोकन की तुलना होती है, लेआउट से ज़्यादा छूने वाला नियम छोड़ दिया जाता है

<p align="center"><img src="../assets/format.png" alt="" width="880"></p>
<p align="center"><img src="../assets/settings-code.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-sql.png" alt="" width="700"></p>
<p align="center"><img src="../assets/settings-wrapping.png" alt="" width="700"></p>

### 2. कॉन्स्टेंट हल करके क्वेरी कॉपी करना

*SQL Tools*

कर्सर को पैकेज, प्रक्रिया या ब्लॉक के भीतर की क्वेरी में रखें और ऐसा स्टेटमेंट पाएँ जो SQL Window या Explain Plan में सीधे चले।

- PL/SQL कॉन्स्टेंट अपने मान में बदलते हैं: `status = c_new` → `status = 'NEW' /*c_new*/`
- वेरिएबल और पैरामीटर बाइंड बन जाते हैं (`:i_customer`), `INTO`, `BULK COLLECT INTO` और `RETURNING ... INTO` हट जाते हैं
- कॉन्स्टेंट PL/SQL की तरह खोजे जाते हैं: ब्लॉक, पैकेज बॉडी, स्पेसिफ़िकेशन; कनेक्शन हो तो दूसरे पैकेज डेटाबेस से पढ़े जाते हैं
- **Explain Plan with constants**: वही स्टेटमेंट IDE की प्लान विंडो में

<p align="center"><img src="../assets/copy-sql.png" alt="" width="880"></p>
<p align="center"><img src="../assets/copy-sql-binds.png" alt="" width="880"></p>

### 3. स्ट्रिंग यूटिलिटी

*String Tools*

जो कुछ आप हाथ से दोबारा टाइप करते हैं, एक क्लिक में: चुने हुए टेक्स्ट पर या पूरे टेक्स्ट पर।

- **लिस्ट → `IN (...)`** लाइनों, `1 2 3`, `a, b` या Excel से; 1000 से ज़्यादा मान भी ORA-01795 नहीं देते
- **केस**: camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, kebab-case (`$`, `#`, सिरिलिक)
- **कोट्स**: एस्केप और वापस, `q'[...]'`, लाइनें → `'a' || chr(10) || 'b'`
- **एन्कोडिंग**: Base64, URL, HEX
- **लाइनें**: सॉर्ट, उलटना, डुप्लिकेट और खाली लाइनें हटाना, अंत के स्पेस काटना, `:=` और `=>` पर संरेखण

<p align="center"><img src="../assets/in-list-big.png" alt="" width="880"></p>
<p align="center"><img src="../assets/string-case.png" alt="" width="480"></p>
<p align="center"><img src="../assets/string-quotes-encode.png" alt="" width="480"></p>

### 4. तारीख़ यूटिलिटी

*Date Tools*

मौजूदा समय और तारीख़ के वे सभी फ़ॉर्मैट जो आपको बार-बार खोजने पड़ते हैं।

- **अभी**: `DATE`, `TIMESTAMP`, `TIMESTAMP WITH TIME ZONE`, `TO_DATE(...)`, ISO 8601, Unix epoch (कनेक्शन हो तो `SYSTIMESTAMP` से)
- **रूपांतरण**: epoch ↔ `TIMESTAMP`, `13.10.2026 14:30` → `TO_DATE(...)` या `DATE` / `TIMESTAMP` लिटरल
- **मास्क**: Java और .NET `yyyy-MM-dd HH:mm` ↔ Oracle `YYYY-MM-DD HH24:MI`, `PT1H30M` → `INTERVAL`

<p align="center"><img src="../assets/date-now.png" alt="" width="880"></p>
<p align="center"><img src="../assets/date-convert.png" alt="" width="880"></p>

उदाहरणों के साथ सभी कमांड: [commands.md](../commands.md)

## इंस्टॉल

1. [नवीनतम रिलीज़](https://github.com/kivilev/plsql-developer-text-tools/releases/latest) से `PlsqlDevTextTools.dll` डाउनलोड करें।
2. PL/SQL Developer बंद करें, DLL को `<PL/SQL Developer>\PlugIns` में कॉपी करें (एडमिन अधिकार चाहिए), फिर IDE शुरू करें।
3. मेन्यू न दिखे तो: **Configure → Plug-Ins**, *Text Tools* चालू करें और रीस्टार्ट करें।

PL/SQL Developer 12–17 x64, हर संस्करण CI में टेस्ट होता है। .NET की ज़रूरत नहीं, नेटवर्क का उपयोग नहीं।
हॉटकी: **Configure → Preferences → User Interface → Key Configuration**, फ़िल्टर `Text Tools`।

---

प्रश्न: [Discussions](https://github.com/kivilev/plsql-developer-text-tools/discussions) · बग: [issues](https://github.com/kivilev/plsql-developer-text-tools/issues/new/choose) · कोड: [CONTRIBUTING](../../CONTRIBUTING.md) · बदलाव: [CHANGELOG](../../CHANGELOG.md)

**Denis Kivilev**: 2005 से बैकएंड डेवलपर, Oracle Certified Professional, [Backend PRO](https://backend-pro.ru) स्कूल के संस्थापक।

**लाइसेंस:** [MIT](../../LICENSE) © 2026 Denis Kivilev

<sub>PL/SQL Developer, Allround Automations का ट्रेडमार्क है; Oracle और PL/SQL, Oracle Corporation के ट्रेडमार्क हैं। यह प्रोजेक्ट किसी से संबद्ध नहीं है।</sub>
