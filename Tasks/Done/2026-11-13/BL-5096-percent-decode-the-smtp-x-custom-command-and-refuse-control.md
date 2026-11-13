---
id: BL-5096
title: Percent-decode the SMTP -X custom command and refuse control characters with exit 3
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpCustomCommandDecodingTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5096 — Percent-decode the SMTP -X custom command and refuse control characters with exit 3

## Goal

`smtp://` with `-X`/`--request` percent-decodes the custom command and refuses a control character (CR, LF, TAB and the like) with exit 3, so one `-X` value can never send several SMTP commands.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-051 (Medium, Confirmed; 4 runs, 8 variants). Weakens security: protocol command injection through the custom-request string; POP3 and IMAP in the same port already reject the same input.

**Repro.**
1. Run an SMTP mock on 127.0.0.1:24990 (PowerShell or a C# file-based app): sends `220 mock` on accept, `250 mock` to EHLO, `221 bye` to QUIT, `250 ok` to anything else, and logs every received line.
2. `curl -sS -m 10 smtp://127.0.0.1:24990/ -X $'NOOP\r\nRSET'; echo $?`
3. `curl -sS -m 10 smtp://127.0.0.1:24990/ -X $'VRFY\r\nRSET' --mail-rcpt c@d`
4. `curl -sS -m 10 smtp://127.0.0.1:24990/ -X 'NOOP%20x'`
5. `curl -sS -m 10 smtp://127.0.0.1:24990/ -X $'NOOP\tx'`

**Expected (curl 8.22.0).** Steps 2, 3 and 5 (and `%0d%0a`, LF alone): `curl: (3) URL using bad/illegal format or missing URL`, exit 3; the server sees only EHLO and QUIT. Step 4: curl sends `NOOP x` (it percent-decodes the SMTP custom request and rejects control characters).

**Actual.** Step 2 exits 0 with `250 ok`; the server receives `NOOP` then `RSET` as two commands. Step 3 sends `VRFY` then `RSET c@d`. Step 4 sends `NOOP%20x` literally. Step 5 passes the TAB.

**Where to start.** `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs`, `SendCommandsAsync` (about line 109): `commandLineText.ToWire(mail.CustomCommand)` with no decoding or validation. Compare `ImapSession.TryDecodeCustomCommand` (`Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs`, about line 595) and `Pop3MessageId.Decode` (`Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Command.cs`, about line 88). Protocol libraries never reference each other, so reproduce the rule inside the SMTP library (or in `Curl.Protocol.Abstractions` only if the run files that as its own task).

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Smtp.UnitTests/SmtpCustomCommandDecodingTests.cs` prove `-X "NOOP\r\nRSET"`, `NOOP%0d%0aRSET`, a lone LF, a lone CR and `NOOP\tx` each end with `CurlExitCode` 3 (`URL using bad/illegal format or missing URL`) and only EHLO and QUIT reach the stand-in server (no custom command is sent).
- [x] A test proves `-X "NOOP%20x"` puts `NOOP x` on the wire, and `-X NOOP` and `-X "VRFY"` with `--mail-rcpt` behave as before.
- [x] Tests are platform-neutral (in-memory connection stand-in, no network).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check: step 2 prints `curl: (3) URL using bad/illegal format or missing URL`, exits 3, and the mock logs only EHLO and QUIT; step 4 puts `NOOP x` on the wire.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 4b2f0e09 8bf1f707 9bf0f82e a0b49a29.
- Decision: followed IMAP's rule (decode %XX, refuse any char < space); reproduced inside the SMTP library. Quality run: 100% line/branch, 0 failing members.
- 2026-11-13: Doing -> Done. SMTP -X is percent-decoded; control characters give exit 3 with only EHLO and QUIT sent.
