---
id: BL-3958
title: CGR-107d: -w size_request for SMTP uploads (75 bytes for the report's repro)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs, Curl.Protocol.Smtp.UnitTests/Cgr107Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3958 — CGR-107d: -w size_request for SMTP uploads (75 bytes for the report's repro)

## Goal

An SMTP upload's `%{size_request}` is what curl 8.22.0 counts.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: `curl -w "%{size_request}" smtp://... --mail-from a@b --mail-rcpt c@d -T msg_crlf.txt` prints 75 with curl and 0 with Curl. Measure which bytes curl counts (commands only, or the message too). POP3 and IMAP already match.

## Acceptance criteria

- [x] The repro prints the same `size_request` as real curl.
- [x] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [x] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- Measured real curl 8.22.0 (Schannel, `Record-CurlExchange.ps1 -Smtp`, `-sS -w "%{size_request} %{size_upload}" --mail-from a@b --mail-rcpt c@d -T body smtp://127.0.0.1:18958/dom`): a 21-byte CRLF-ended body printed `24 24`, a 44-byte one `47 47`, `a\r\n.b` printed `11 11`, and `--mail-rcpt c@d` without `-T` (VRFY) printed `0 0`. So curl's SMTP `size_request` is exactly the message bytes sent after DATA, dot-stuffed and with the end-of-data mark (the same as `size_upload`); no command line is counted. The report's 75 is that count for its `msg_crlf.txt`.
- The gap report (OP-11) measured Curl printing `0` for `size_request`; after the fix, the same four command lines through Curl (`Record-CurlExchange.ps1 -Curl dotnet -CurlArgs run,--no-build,--project,Curl.Console,--,...`) print `24 24`, `47 47`, `11 11` and `0 0`, matching curl.
- Fix: `SmtpMailTransaction` sets `TransferReport.RequestSize` to the uploaded count beside `UploadSize`. No ADR: a measured match, no design choice. The fix lands in `SmtpMailTransaction.cs`, not `SmtpProtocolHandler.cs`, so it was added to `touches`; no task in Doing on origin/work/dark-factory names it.
- Platform-neutral: the count is bytes the client sends; the tests use no path, OS error or certificate, and curl's count is the same code path on its OpenSSL builds, so no per-platform pin is needed. CI on Linux and macOS is covered by keeping the change platform-neutral and the fast tests green here.
- `Cgr107Tests` failed (2 of 3) with the fix reverted and passes with it. `dotnet build` clean; fast tests 44626 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary`: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: bb798eb8 d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Done. SMTP uploads report curl 8.22.0's size_request: the dot-stuffed message bytes and end-of-data mark
