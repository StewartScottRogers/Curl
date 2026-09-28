---
id: BL-540
title: Open an SMTP session: greeting, EHLO or HELO, STARTTLS, smtps and QUIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-529]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-540 — Open an SMTP session: greeting, EHLO or HELO, STARTTLS, smtps and QUIT

## Goal

An `SmtpProtocolHandler` in `Curl.Protocol.Smtp.UnitLibrary` connects through `IConnector`, reads the greeting and multi-line replies, sends `EHLO <domain>` (falling back to `HELO` as curl does), upgrades with `STARTTLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `smtps://`, ends with `QUIT`, and maps every failure to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; SMTP split: this session, BL-541 auth, BL-542 upload, BL-543 non-upload commands, BL-544 options, BL-545 registration, BL-546 `-v`).
- Design: BL-533's ADR (STARTTLS, security levels, URL model: the path names the `EHLO` domain). Contract: BL-534. Timeouts: follow BL-498's ADR; endpoints: BL-515's ADR.
- `Curl.Protocol.Smtp.UnitLibrary/CLAUDE.md`: reference only Abstractions, take `IConnection`, never a `Socket`/`SslStream`/`HttpClient`. The FTP handler's `AUTH TLS` upgrade through `ITlsProvider` is the model for `STARTTLS`.
- Measure with `Record-CurlExchange.ps1 -Smtp` (BL-529): the default session with `-T`, a greeting of `554`, `EHLO` answered `502` (HELO fallback), `STARTTLS` refused under `--ssl` and under `--ssl-reqd` (exit 64), `smtps://` with `-Tls -k`, a reply line that is not a reply (exit 8), and the server closing early.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout, stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` drive the handler through a fake `IConnector`/`IConnection` replaying the measured server bytes and pin the client bytes and the exit code and message for each case, including multi-line replies split across reads.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, 2026-09-28, `Record-CurlExchange.ps1 -Smtp`)

curl ran `-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/client.example`
unless stated. stdout was empty in every case. `>` is curl, `<` the server; the upload
(`MAIL FROM:<a@b> SIZE=21`, `RCPT TO:<c@d>`, `DATA`, body, `.`) is abbreviated "upload".

| Case | Client lines | Exit | stderr |
| --- | --- | --- | --- |
| default | `EHLO client.example`, upload, `QUIT` | 0 | - |
| `GREETING=554 go away` | nothing | 8 | `curl: (8) Got unexpected smtp-server response: 554` |
| `EHLO=502 no` (also `421`) | `EHLO client.example`, `HELO client.example`, upload (no SIZE), `QUIT` | 0 | - |
| `EHLO=502`, `HELO=501 no` | `EHLO`, `HELO`, no QUIT | 9 | `curl: (9) Remote access denied: 501` |
| `--ssl-reqd`, `EHLO=502 no` | `EHLO`, no HELO, no QUIT | 9 | `curl: (9) Remote access denied: 502` |
| `--ssl`, `STARTTLS=454 not now` | `EHLO`, `STARTTLS`, upload (no second EHLO), `QUIT` | 0 | - |
| `--ssl-reqd`, `STARTTLS=454 not now` | `EHLO`, `STARTTLS`, no QUIT | 64 | `curl: (64) STARTTLS denied, code 454` |
| `--ssl-reqd`, EHLO without STARTTLS | `EHLO`, no QUIT | 64 | `curl: (64) STARTTLS not supported.` |
| `--ssl-reqd -k`, STARTTLS 220 | `EHLO`, `STARTTLS`, TLS, `EHLO client.example`, upload, `QUIT` | 0 | - |
| `--ssl-reqd` without `-k` | `EHLO`, `STARTTLS`, handshake fails, no QUIT | 60 | `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT ...` (the TLS provider's) |
| `smtps://` with `-Tls -k` | `EHLO client.example`, upload, `QUIT` | 0 | - |
| `GREETING=hello there` (not a reply line) | nothing; curl skips the line and waits; server hangs up after 2 s | 56 | `curl: (56) response reading failed (errno: 0)` |
| `EHLO=CLOSE` (server closes early) | `EHLO` | 56 | `curl: (56) response reading failed (errno: 0)` |
| `QUIT=500 no`, `QUIT=CLOSE` | as default | 0 | - |
| multi-line greeting `220-first`/`220-second`/`220 last` | as default | 0 | - |
| `GREETING=junk\r\n220 ok` | as default | 0 | - |
| `GREETING=220` (bare, 3 digits and CRLF) | as default | 0 | - |
| greeting line 65533 chars + CRLF | as default | 0 | - |
| greeting line 65534 chars + CRLF | nothing | 100 | `curl: (100) A value or data field grew larger than allowed` |
| EHLO `250 starttls` / `250 STARTTLSX` / `251-STARTTLS` under `--ssl-reqd -k` | `STARTTLS` sent in all three | 0 | - |
| EHLO lines ending LF only | parsed as with CRLF | 0 | - |
| `smtp://h/a%20b` / `caf%C3%A9` / `a%zz` | `EHLO a b` / `EHLO caf` + bytes C3 A9 / `EHLO a%zz` | 0 | - |
| `smtp://h/a%0Db`, `a%00b` | connects, sends nothing | 3 | `curl: (3) URL using bad/illegal format or missing URL` |
| `-T - smtp://h/` (empty path) | `EHLO Stewart-Rogers-AI-PC` (the machine's host name) | 0 | - |
| `-T mail.txt smtp://h/` | `EHLO smtp540mail.txt` (the command line appends the file name) | 0 | - |

### Plan and decisions

- `SmtpProtocolHandler(IConnector, ITlsProvider)` connects (TLS for `smtps`, ports from
  `CurlUrl.Port`: 25/465), decodes the EHLO domain (`SmtpEhloDomain`), then `SmtpSession`
  runs greeting, EHLO/HELO, STARTTLS and QUIT; `SmtpControlChannel` is the line reader
  (ADR-0121 §7). Every rule is curl's measured behaviour above, so no ADR was needed.
- Until BL-541/BL-542/BL-543 land, the handler closes an opened session with `QUIT` and
  reports success; the handler's remarks say so. It is not registered in `Curl.Console`
  yet (BL-545), so no user can reach this interim behaviour.
- A failed write is not reported on its own: the next read finds the connection closed and
  the session fails 56. curl's exit for a send failure was not measurable with the recorder.
- The empty-path host name comes from `Dns.GetHostName()` (curl's `gethostname`); an
  internal constructor injects it for tests.
- Pipeline stages run in-session: plan, tests, implementation, verify and quality gate. No
  separate reviewer or conformance agent was run for this task; every pinned byte comes
  from the measurements above.

### Results

- 44 tests in `Curl.Protocol.Smtp.UnitTests`, all fake connections, no Integration category.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary`: 100% line, 100%
  branch, 38 members, 0 failing, worst CRAP 10.
- `dotnet build Curl.slnx -warnaserror` clean; fast tests: 9649 passed, 0 failed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SmtpProtocolHandler opens smtp/smtps sessions: greeting, EHLO with HELO fallback, STARTTLS per --ssl/--ssl-reqd, QUIT, with curl 8.21.0's exit codes and messages
