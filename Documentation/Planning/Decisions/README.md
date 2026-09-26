# Architecture Decision Records

One file per decision, named `ADR-NNNN-short-slug.md`. Numbers are assigned in
order and never reused.

An ADR is immutable once **Accepted**. A decision that changes does not get edited
— a new ADR supersedes it, and the old one is marked `Superseded by ADR-NNNN`.
The value of the record is that it shows what was believed *at the time*.

Write one when a choice is expensive to reverse, when a reasonable person would
have chosen differently, or when the reasoning would otherwise be lost. Routine
choices do not need one.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](ADR-0001-adopt-slnx-solution-format.md) | Adopt the `.slnx` solution format and a shared project for documentation | Accepted | 2026-09-25 |
| [0002](ADR-0002-ifilesystem-as-the-second-protocol-seam.md) | `IFileSystem` as the second protocol seam | Accepted | 2026-09-25 |
| [0003](ADR-0003-itransfercontext-carries-transfer-options.md) | `ITransferContext` carries transfer options | Accepted | 2026-09-25 |
| [0004](ADR-0004-upload-file-name-percent-encoded-as-utf8.md) | The `-T` file name appended to a URL is percent-encoded as UTF-8 | Proposed | 2026-09-26 |
| [0005](ADR-0005-protocol-handlers-acquire-transports-through-connectors.md) | Protocol handlers acquire transports through connectors | Accepted | 2026-09-26 |
| [0006](ADR-0006-transfer-context-carries-phase-4-protocol-options.md) | The transfer context carries the Phase 4 protocol options | Accepted | 2026-09-26 |

## Template

```markdown
# ADR-NNNN — <title>

- **Status:** Proposed / Accepted / Superseded by ADR-NNNN
- **Date:** YYYY-MM-DD

## Context
The forces at play. What made a decision necessary.

## Decision
What was chosen, stated as a decision rather than a description.

## Consequences
What this makes easy, and what it makes hard. Both, honestly.

## Alternatives considered
Each option and the specific reason it lost.
```
