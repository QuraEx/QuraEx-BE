# QuraEx v2 — Documentation

Start with the root [`README.md`](../README.md) to run the stack, then come back here.

| Doc | Purpose |
|-----|---------|
| [`TASKS.md`](./TASKS.md) | Service build order, owners, dependencies, event contracts. Claim a service here. |
| [`database/conventions.md`](./database/conventions.md) | Authoritative DB rules (naming, soft-delete, outbox, golden flow) — same for every service. |
| [`database/quraex.dbml`](./database/quraex.dbml) | Master schema, source of truth. Render at [dbdiagram.io](https://dbdiagram.io). |
| [`QuraEx_Architecture.md`](./QuraEx_Architecture.md) | System design, service boundaries, messaging topology. |
| [`QuraEx_SRS.md`](./QuraEx_SRS.md) | Software Requirements Specification — product scope and requirements. |

`.docx` copies of the SRS and Architecture docs are kept alongside the Markdown
versions for stakeholders who prefer Word. The Markdown files are the canonical,
diff-friendly source.

For commit/PR rules and the step-by-step recipe to add a service, see
[`../CONTRIBUTING.md`](../CONTRIBUTING.md).
