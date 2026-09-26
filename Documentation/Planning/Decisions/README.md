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
| [0007](ADR-0007-file-handler-keeps-negative-resume-guard.md) | `FileProtocolHandler` keeps its negative-`ResumeFrom` guard as an unreachable defensive default | Accepted | 2026-09-26 |
| [0008](ADR-0008-transfer-context-carries-connect-timeout-and-max-time.md) | The transfer context carries the connect timeout and the maximum time | Accepted | 2026-09-26 |
| [0009](ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md) | TLS failure messages, `--capath`, `--cert` formats and the trust store match the platform's usual curl build | Accepted | 2026-09-26 |
| [0010](ADR-0010-representing-urls-system-uri-cannot-round-trip.md) | Representing URLs `System.Uri` cannot round-trip: replace, wrap or pre-parse | Proposed | 2026-09-26 |
| [0011](ADR-0011-cipher-options-follow-schannel-on-windows-and-are-honoured-elsewhere.md) | `--ciphers` and `--tls13-ciphers`: Schannel behaviour on Windows, honoured through `CipherSuitesPolicy` on Linux and macOS | Accepted | 2026-09-26 |
| [0012](ADR-0012-relicense-from-gpl-3-0-to-mit.md) | Relicense from GPL-3.0 to MIT | Accepted | 2026-09-26 |
| [0013](ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md) | curl's upstream test cases run as data-driven MSTest cases, in process | Accepted | 2026-09-26 |
| [0014](ADR-0014-http-request-options-and-the-auth-cookie-and-proxy-seams.md) | HTTP request options and the auth, cookie and proxy seams | Accepted | 2026-09-26 |
| [0015](ADR-0015-transfer-report-on-transfer-result-and-connect-timings-on-connect-result.md) | `TransferReport` on `TransferResult` and `ConnectTimings` on `ConnectResult` | Accepted | 2026-09-26 |
| [0016](ADR-0016-authentication-and-cookies-move-into-milestone-1.md) | `Curl.Authentication` and `Curl.Cookies` move into Milestone 1 | Accepted | 2026-09-26 |

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
