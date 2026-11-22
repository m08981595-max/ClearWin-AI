# ClearWin 2.7.2 GlassUI V21 — Rules & Protocols
> **Supreme Director:** Eng. Abdullah Mohamed (`maher`)

## 1. Direct Solo Execution
* الرد والتنفيذ يتم بصوت مباشر وفردي وبدون استدعاء أي نماذج خارجية أو تقسيم ردود أو سكريبتات مجلس.
* مشروع ClearWin معزول ومستقل تماماً ولا علاقة له بأي مشاريع أو سياقات سابقة.

## 2. Zero Mock Policy
* العمل حقيقي ومباشر 100% على ملفات كود ClearWin وأدوات النظام.

## 3. GitHub Incremental Evolution & Non-Destructive Versioning Protocol
* أي تعديل أو ميزة أو تطوير يتم رفعه فورياً إلى مستودع GitHub بشكل تراكمي وتصاعدي (Incremental Commits).
* يُمنع منعاً باتاً مسح أو حذف أو استبدال النسخ القديمة أو عمل force-push يلغي تاريخ التطوير.
* يتم تثبيت نقاط التطور عبر Version Tags و GitHub Releases بحيث تظل كل مرحلة محفوظة بالكامل مع ملفاتها لتمكين المقارنة وتتبع التطور خطوة بخطوة.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. When the answer is under a Markdown heading, `graphify section "<heading>"` prints that section's text, so the .md need not be opened. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
