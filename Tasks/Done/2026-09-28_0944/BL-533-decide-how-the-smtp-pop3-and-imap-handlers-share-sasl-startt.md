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
completed: 2026-09-28
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

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed.
- [x] It names: the SASL contract (interface name and members) and its home project; the new `ITransferContext` members with types; how STARTTLS/STLS and implicit TLS work per scheme and security level; the URL model per scheme; the SASL mechanism preference order with its measurement; and whether any line-reading code is shared (and where) or duplicated per library.
- [x] Its Consequences list the tasks it unblocks (BL-534 to BL-559).
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decision: ADR-0121 (0121 was the next free number on this branch and on every branch
  checked). SASL is `ISaslAuthenticator`/`ISaslExchange`/`SaslRequest` in Abstractions,
  implemented in `Curl.Authentication.UnitLibrary`, following ADR-0014. This matches the
  `touches` BL-534 and BL-536 to BL-538 were already filed with. The options are one
  `MailRequestOptions? Mail` record on `ITransferContext`, like `Http`. Each library keeps
  its own line reader.
- Measured curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Smtp/-Pop3/-Imap`: the
  mechanism order, the GSSAPI/bearer/EXTERNAL/authzid conditions, `--sasl-ir`, `AUTH=*`,
  an unavailable `AUTH=`, `--login-options` winning over URL `;AUTH=`, the POP3 SASL -> APOP
  -> USER and IMAP LOGIN fallbacks, and STARTTLS under `--ssl`/`--ssl-reqd`. The ADR's
  Context holds the results.
- Written directly rather than through `align-and-document`, because the measurements
  and the decision were already in this session. No `.cs` or project file changed, so
  `verify` only needs the build and the fast tests.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0121 fixes the SASL contract, mail options record, STARTTLS rules, URL models and measured mechanism order for SMTP, POP3 and IMAP
