---
id: BL-533
title: Decide how the SMTP, POP3 and IMAP handlers share SASL, STARTTLS and their options
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-533 — Decide how the SMTP, POP3 and IMAP handlers share SASL, STARTTLS and their options

## Goal

An ADR fixes the shape of the three mail handlers before any is written: where SASL mechanisms live given that protocol libraries may not reference each other, how STARTTLS/STLS upgrades a connection through `ITlsProvider`, how `--ssl`/`--ssl-reqd` map (`TransportSecurityLevel`), which new `ITransferContext` members carry the mail options, and how each URL is read (curl's `smtp://host/<domain>`, `pop3://host/<msgnum>`, `imap://host/<mailbox>;UID=<n>/;SECTION=<s>?<search>`).

## Context

- Conformance audit 2026-09-28, rows 34 (Blocker, L each: `smtp(s)`, `imap(s)`, `pop3(s)`; projects empty), 31 (mail options) and 23 (`--sasl-authzid`, `--sasl-ir`, `--login-options`).
- Projects: `Curl.Protocol.Smtp.UnitLibrary`, `Curl.Protocol.Pop3.UnitLibrary`, `Curl.Protocol.Imap.UnitLibrary` (each with a `CLAUDE.md`: reference only Abstractions, take `IConnection`, never a `Socket`/`SslStream`). HTTP authentication already crosses the boundary through `IHttpAuthenticator` in Abstractions, implemented in `Curl.Authentication.UnitLibrary` and injected by `Curl.Console` (ADR-0014); SASL can follow that pattern.
- FTP already upgrades a control connection with `AUTH TLS` through `ITlsProvider` (ADR-0102) and reads `TransportSecurityLevel SslLevel` from the context; reuse that.
- Depends on BL-498 (the timeout contract new handlers follow) and BL-515 (how endpoints reach `%{local_ip}` and friends), so the ADR can state what a mail handler must do for each.
- Upstream: https://curl.se/docs/manpage.html (8.21.0 reference; `CurlManual.txt` embeds it), https://everything.curl.dev/usingcurl/smtp, `/pop3`, `/imap`, and RFC 5321, RFC 1939, RFC 3501, RFC 4422, RFC 4954, RFC 5034. SASL mechanism preference is curl's (measure it with the `-Smtp` recorder, BL-529, offering several mechanisms, and record the order).

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed.
- [ ] It names: the SASL contract (interface name and members) and its home project; the new `ITransferContext` members with types; how STARTTLS/STLS and implicit TLS work per scheme and security level; the URL model per scheme; the SASL mechanism preference order with its measurement; and whether any line-reading code is shared (and where) or duplicated per library.
- [ ] Its Consequences list the tasks it unblocks (BL-534 to BL-559).
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
