## 1. Purpose

This document describes the complete structure, organization, and allocation of files inside the `AI-Knowledge` directory.

It serves as the central navigation document for AI models working with this knowledge base.

Every AI model must read this document before searching, creating, updating, or deleting any Knowledge files.

### Objectives

- Provide a centralized map of all Knowledge files.
- Identify which task (Task 00–07) is responsible for generating each file.
- Explain the purpose and expected content of each file.
- Define relationships between documentation, indexes, modules, and features.
- Prevent duplicate documentation and inconsistent information.
- Support incremental Knowledge updates when source code changes.

---

## 2. Directory Structure

```text
AI-Knowledge/
│
├── AI-KNOWLEDGE-STRUCTURE.md
│
├── 00-OVERVIEW/
│   ├── PROJECT.md
│   ├── ARCHITECTURE.md
│   ├── TECH-STACK.md
│   ├── CODING-RULES.md
│   ├── SOURCE-INVENTORY.md
│   ├── PROJECT-INVENTORY.json
│   ├── MODULE-INVENTORY.json
│   └── PROJECT-DEPENDENCY-DIAGRAM.md
│
├── 01-MODULES/
│   ├── HOTEL.md
│   ├── BUS.md
│   ├── FERRY.md
│   ├── BOOKING.md
│   └── PAYMENT.md
│
├── 02-DATABASE/
│   ├── DATABASE-OVERVIEW.md
│   ├── TABLE-RELATIONSHIPS.md
│   └── TABLE-INDEX.json
│
├── 03-CODE-INDEX/
│   ├── FILE-INDEX.jsonl
│   ├── SYMBOL-INDEX.jsonl
│   ├── DEPENDENCY-INDEX.jsonl
│   └── ROUTE-INDEX.jsonl
│
├── 04-INTEGRATIONS/
│   ├── INTEGRATION-OVERVIEW.md
│   └── EXTERNAL-APIS.md
│
├── 05-FEATURES/
│   ├── HOTEL-SEARCH.md
│   ├── HOTEL-BOOKING.md
│   ├── BUS-BOOKING.md
│   └── PAYMENT-CHECKOUT.md
│
└── 06-UPDATE-LOG/
    ├── KNOWLEDGE-BASELINE.json
    ├── UPDATE-LOG.md
    └── PENDING-UPDATES.md
```

**Important:** The module and feature names above are illustrative examples. Actual files must be created based on modules and features verified from the source code. Do not create documentation for modules that do not exist.

---

## 3. Task-to-File Mapping

This section defines which output files belong to each generation task.

| Task | Description | Output directory |
|---|---|---|
| Task 00 | Global instructions and analysis rules | Applies to all directories |
| Task 01 | Source inventory and classification | `00-OVERVIEW/` |
| Task 02 | Project overview and architecture | `00-OVERVIEW/` |
| Task 03 | Module reverse engineering | `01-MODULES/` |
| Task 04 | Feature-level documentation | `05-FEATURES/` |
| Task 05 | Structured code indexes | `03-CODE-INDEX/` |
| Task 06 | Database and integration documentation | `02-DATABASE/`, `04-INTEGRATIONS/` |
| Task 07 | Validation and quality audit | Audit reports and correction outputs; does not replace the main Knowledge files |

### Task 00 — Global Instructions

Task 00 defines the rules that all subsequent tasks must follow.

It is a prompt/instruction task, not a business documentation output task.

Responsibilities:

- Establish source-code-first principles.
- Define naming conventions.
- Define evidence and confidence levels.
- Prevent hallucinated classes, methods, routes, and relationships.
- Define output formats.
- Define how incomplete or uncertain information must be reported.

The instructions should be reused for Tasks 01–07.

### Task 01 — Source Inventory

Output directory: `00-OVERVIEW/`

| File | Purpose |
|---|---|
| `SOURCE-INVENTORY.md` | Human-readable inventory of source files and their classifications. |
| `PROJECT-INVENTORY.json` | Structured project inventory, including project names, paths, frameworks, and project types. |
| `MODULE-INVENTORY.json` | List of identified modules, their responsibilities, and associated source locations. |
| `MISSING-SOURCE-REPORT.md` | Reports missing, inaccessible, or unclassified source information. |

### Task 02 — Project Overview and Architecture

Output directory: `00-OVERVIEW/`

| File | Purpose |
|---|---|
| `PROJECT.md` | General project description, business purpose, scope, and major capabilities. |
| `ARCHITECTURE.md` | Overall software architecture, architectural styles, layers, and communication patterns. |
| `TECH-STACK.md` | Languages, frameworks, libraries, databases, and infrastructure technologies. |
| `CODING-RULES.md` | Existing coding conventions and implementation patterns observed in the source. |
| `PROJECT-DEPENDENCY-DIAGRAM.md` | Project-level dependency and reference relationships. |

These documents describe the system as a whole rather than individual business features.

### Task 03 — Module Reverse Engineering

Output directory: `01-MODULES/`

One Markdown file per verified business or technical module.

Examples:

| File | Purpose |
|---|---|
| `HOTEL.md` | Hotel module responsibilities, components, workflows, and dependencies. |
| `BUS.md` | Bus module responsibilities, components, workflows, and dependencies. |
| `FERRY.md` | Ferry module responsibilities, components, workflows, and dependencies. |
| `BOOKING.md` | Shared booking capabilities and related components. |
| `PAYMENT.md` | Payment module responsibilities and integration points. |

Each module document should describe:

- Module purpose and scope.
- Main Controllers, Services, Repositories, Models, and Views.
- Important classes and methods.
- Internal dependencies.
- External module dependencies.
- Main business workflows.
- Routes and entry points.
- Related database objects.
- Related external integrations.
- Known limitations and uncertain information.

Do not duplicate the entire feature workflow in the module document. Link to the corresponding feature document instead.

### Task 04 — Feature-Level Documentation

Output directory: `05-FEATURES/`

One Markdown file per verified business feature.

Examples:

| File | Purpose |
|---|---|
| `HOTEL-SEARCH.md` | Hotel search workflow from user request to search result. |
| `HOTEL-BOOKING.md` | Hotel booking workflow, validation, pricing, and confirmation. |
| `BUS-BOOKING.md` | Bus booking workflow and ticket processing. |
| `PAYMENT-CHECKOUT.md` | Checkout and payment processing workflow. |

Each feature document should describe:

- Business objective.
- Entry point and user actions.
- End-to-end processing flow.
- Controllers and Actions.
- Services and methods involved.
- Repository and database interactions.
- External API calls.
- Validation and business rules.
- Error handling.
- Output and final state.
- Dependencies on other features or modules.

Feature documents should explain how the system behaves, not just list classes.

### Task 05 — Structured Code Indexes

Output directory: `03-CODE-INDEX/`

These files provide machine-readable information for precise code retrieval.

| File | Purpose |
|---|---|
| `FILE-INDEX.jsonl` | Index of source files, paths, project ownership, file types, and hashes. |
| `SYMBOL-INDEX.jsonl` | Index of classes, interfaces, methods, properties, and other symbols. |
| `DEPENDENCY-INDEX.jsonl` | Relationships between classes, methods, projects, and other code elements. |
| `ROUTE-INDEX.jsonl` | MVC routes, Controllers, Actions, HTTP methods, and route patterns. |

Rules:

- Use JSONL format: one valid JSON object per line.
- Keep actual source paths and symbol names.
- Preserve namespaces and project ownership.
- Record evidence type and confidence for dependency relationships.
- Distinguish confirmed relationships from convention-based or inferred relationships.
- Do not store invented or unverified references.

### Task 06 — Database and Integration Documentation

Output directories: `02-DATABASE/` and `04-INTEGRATIONS/`

**Database outputs**

| File | Purpose |
|---|---|
| `DATABASE-OVERVIEW.md` | Database technologies, schemas, major entities, and data-access patterns. |
| `TABLE-RELATIONSHIPS.md` | Relationships between tables, keys, and important data flows. |
| `TABLE-INDEX.json` | Structured database table, column, key, and relationship metadata. |

**Integration outputs**

| File | Purpose |
|---|---|
| `INTEGRATION-OVERVIEW.md` | Overview of internal and external system integrations. |
| `EXTERNAL-APIS.md` | External API endpoints, protocols, authentication approaches, request/response structures, and calling source locations. |

Database and integration documentation must be based on actual source code, SQL schema, configuration structure, or other supplied technical evidence.

Never include real passwords, API keys, access tokens, or production secrets.

### Task 07 — Validation and Quality Audit

Task 07 validates the outputs from Tasks 01–06.

It is an audit task, not a replacement documentation task.

Suggested audit artifacts:

| File | Purpose |
|---|---|
| `KNOWLEDGE-VALIDATION-REPORT.md` | Overall quality and accuracy assessment. |
| `KNOWLEDGE-ISSUES.jsonl` | Machine-readable list of identified documentation issues. |
| `MISSING-DATA-REPORT.md` | Missing or unverifiable information. |
| `CORRECTION-PLAN.md` | Recommended corrections and priorities. |

These are audit artifacts. They may be stored temporarily outside the main Knowledge directories or in a separate audit folder.

Only merge validated corrections into the main Knowledge files.

---

## 4. File Responsibilities and Sources of Truth

Different files serve different purposes. Avoid treating all documentation as equally authoritative.

| Information | Primary source of truth |
|---|---|
| Actual class and method names | Source code |
| File locations | Source code repository |
| Controller routes and actions | Source code and route configuration |
| Project references | `.sln` and project files |
| Database tables and columns | Database schema and migrations |
| External API behavior | API documentation and actual integration code |
| Overall architecture | Source code analysis, supported by project documentation |
| Business workflows | Source code analysis and verified business documentation |
| Code dependency relationships | Semantic analysis and explicit source references |

When two Knowledge files conflict, verify against the relevant source code or schema before updating either file.

---

## 5. Relationships Between Knowledge Directories

The directories are complementary and should reference each other.

| Directory | Main relationship |
|---|---|
| `00-OVERVIEW/` | Provides the overall system context for all other directories. |
| `01-MODULES/` | Explains module boundaries and links to related features. |
| `02-DATABASE/` | Describes the database objects used by modules and features. |
| `03-CODE-INDEX/` | Provides exact locations and machine-readable references to source code. |
| `04-INTEGRATIONS/` | Describes external systems used by modules and features. |
| `05-FEATURES/` | Describes end-to-end business workflows and links to modules, code, database, and integrations. |
| `06-UPDATE-LOG/` | Tracks changes to the Knowledge base over time. |

Example:

A `HOTEL-BOOKING.md` feature document may reference:

- Module: `01-MODULES/HOTEL.md`
- Architecture: `00-OVERVIEW/ARCHITECTURE.md`
- Code symbols: `03-CODE-INDEX/SYMBOL-INDEX.jsonl`
- Routes: `03-CODE-INDEX/ROUTE-INDEX.jsonl`
- Database: `02-DATABASE/TABLE-INDEX.json`
- External APIs: `04-INTEGRATIONS/EXTERNAL-APIS.md`

Use relative paths where possible.

---

## 6. Naming and Formatting Conventions

### Markdown files

- Use uppercase names.
- Separate words with hyphens.
- Use `.md` extension.
- Keep one main subject per file.
- Avoid creating multiple files with overlapping responsibilities.

Examples:

- `HOTEL-BOOKING.md`
- `PAYMENT-CHECKOUT.md`
- `DATABASE-OVERVIEW.md`

### JSON files

- Use valid JSON syntax.
- Use UTF-8 encoding.
- Use consistent property naming.
- Include identifiers and source paths where applicable.

### JSONL files

- One JSON object per line.
- No surrounding JSON array.
- Each line must be independently valid JSON.
- Preserve stable identifiers where possible.

### Source references

When documenting code, provide:

- Relative source path.
- Namespace.
- Class or interface name.
- Method or property name.
- Line range when available.
- Evidence type when describing dependencies.

Line numbers may become outdated after source changes. Always verify them against the current source before relying on them.

---

## 7. Incremental Update Rules

When the source code changes, update only the affected Knowledge files.

Recommended workflow:

1. Detect added, modified, deleted, and renamed source files.
2. Identify affected symbols and dependencies.
3. Identify impacted modules and features.
4. Update relevant code indexes.
5. Update relevant module and feature documents.
6. Update database and integration documents if necessary.
7. Run validation.
8. Update the Knowledge baseline only after successful validation and approval.

### Update mapping

| Source change | Knowledge files to review |
|---|---|
| New project or project reference | Project inventory, architecture, dependency diagram |
| New or modified Controller | Symbol index, route index, affected module and feature documents |
| New or modified Service | Symbol index, dependency index, affected module and feature documents |
| New or modified Model | Symbol index, affected database and module documentation |
| New or modified Razor View | File index, affected feature documentation |
| Database schema change | Table index, database overview, table relationships |
| External API change | Integration overview, external APIs, affected feature documents |
| File deletion or rename | File index, symbol index, dependency index, affected documentation |

A change to one source file may affect several Knowledge files. Use dependency and impact analysis rather than relying only on this mapping.

---

## 8. Update Log and Baseline

The `06-UPDATE-LOG/` directory tracks synchronization between source code and Knowledge.

### KNOWLEDGE-BASELINE.json

Stores the last successfully validated source commit.

Example:

```json
{
  "solution": "Easybook",
  "lastIndexedCommit": "abc123",
  "lastUpdatedAt": "2026-11-04T10:00:00+07:00",
  "status": "completed"
}
```

### UPDATE-LOG.md

Records completed Knowledge update sessions, including:

- Date and time.
- Source commit.
- Changed modules and features.
- Knowledge files created, modified, or deleted.
- Validation result.
- Outstanding issues.

### PENDING-UPDATES.md

Tracks changes that have not yet been reflected in Knowledge.

Do not advance the baseline while required Knowledge updates remain incomplete.

---

## 9. Mandatory Instructions for AI Models

Whenever an AI model works with this Knowledge base, it must follow these rules:

1. Read `AI-KNOWLEDGE-STRUCTURE.md` first.
2. Read only the relevant Knowledge files for the requested task.
3. Use source code as the authority for actual implementation behavior.
4. Use module documents for module-level context.
5. Use feature documents for end-to-end business workflows.
6. Use code indexes to locate exact source files and symbols.
7. Use database and integration documentation when relevant.
8. Do not assume a file exists just because it is listed as an example.
9. Do not invent missing business rules, methods, routes, dependencies, or database structures.
10. Clearly label information as unverified when evidence is insufficient.
11. Avoid duplicating existing documentation.
12. When updating Knowledge, preserve content that remains correct.
13. Update this structure document if the directory organization or file responsibilities change.
14. Never include credentials, secrets, or production personal data.

---

## 10. Recommended Reading Strategy for AI Tasks

Do not load the entire Knowledge base for every request.

Select documents based on the task.

| User task | Recommended Knowledge files |
|---|---|
| Understand the whole system | `PROJECT.md`, `ARCHITECTURE.md`, `TECH-STACK.md` |
| Debug a specific Controller | Relevant module, feature, symbol index, dependency index, route index |
| Add a new business feature | Architecture, related module, similar feature, symbol and dependency indexes |
| Modify hotel booking | `HOTEL.md`, `HOTEL-BOOKING.md`, relevant database and integration documents |
| Modify database schema | Database overview, table relationships, table index, affected module documents |
| Modify an external API | Integration overview, external APIs, affected feature document |
| Investigate cross-module dependencies | Architecture, project dependency diagram, module documents, dependency index |

The AI model should retrieve additional source code only when the Knowledge files do not provide sufficient evidence.

---

## 11. Maintenance Principle

The `AI-Knowledge` directory is a continuously maintained representation of the actual software system.

It is not a one-time generated report.

The desired state is:

- Accurate enough to guide development and debugging.
- Structured enough for different AI models to consume.
- Small enough to avoid unnecessary context tokens.
- Traceable to actual source code.
- Updated incrementally as the system evolves.

**Final principle:**

> Source code defines what the system actually does. AI-Knowledge explains where the implementation is, how its components relate, and how its business workflows operate.