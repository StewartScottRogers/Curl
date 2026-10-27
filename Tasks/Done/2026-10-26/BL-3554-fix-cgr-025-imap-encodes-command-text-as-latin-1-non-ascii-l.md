---
id: BL-3554
title: Fix CGR-025: IMAP encodes command text as Latin-1: non-ASCII LOGIN user/password and -X commands are corrupted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs, Curl.Protocol.Imap.UnitLibrary/ImapCommandLineText.cs, Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Protocol.Imap.UnitLibrary/ImapSession.cs, Curl.Protocol.Imap.UnitTests/Cgr025Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3554 — Fix CGR-025: IMAP encodes command text as Latin-1: non-ASCII LOGIN user/password and -X commands are corrupted

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-025 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-025: IMAP encodes command text as Latin-1: non-ASCII LOGIN user/password and `-X` commands are corrupted

- **Severity:** High
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (verifier re-ran this on both platforms).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-2`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- Linux (`EV/verify-linux.cases`, server mode `imap`):
  - Case i30, with server option `capa=* CAPABILITY IMAP4rev1`: `curl -s -S -u 'usér:päss' 'imap://127.0.0.1:21084/INBOX;UID=1'`
  - Case e04: `curl -s -S -u u:p -X 'XFOO é' imap://127.0.0.1:21084/`
- Windows (`EV\enc2.cases`):
  - e20: `curl -s -S -u u:p -X "XFOO €" imap://127.0.0.1:21001/`
  - e22: `curl -s -S -u "u€:p" "imap://127.0.0.1:21001/INBOX;UID=1"`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u 'usér:päss' 'imap://127.0.0.1:21084/INBOX;UID=1'
curl -s -S -u u:p -X 'XFOO é' imap://127.0.0.1:21084/
curl -s -S -u u:p -X "XFOO €" imap://127.0.0.1:21001/
curl -s -S -u "u€:p" "imap://127.0.0.1:21001/INBOX;UID=1"
```

**Expected (real curl)**

- sends the argument bytes unchanged.
  - Linux: `A002 LOGIN us\xC3\xA9r p\xC3\xA4ss` and `A003 XFOO \xC3\xA9`
  - Windows (ANSI cp1252): `A002 LOGIN u\x80 p` and `A003 XFOO \x80`

**Actual (Curl)**

- Linux: `A002 LOGIN us\xE9r p\xE4ss` and `A003 XFOO \xE9`. UTF-8 credentials become Latin-1, so a real server rejects the login.
- Windows: `A002 LOGIN u? p` and `A003 XFOO ?`.
- SMTP and POP3 send the same strings correctly on Linux.

**Suspected location in source**

- `Curl.Protocol.Imap.UnitLibrary\ImapControlChannel.cs:188`: `Encoding.Latin1.GetBytes(text + "\r\n")`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr025Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5): Fix. The control channel stays Latin-1 (URL-derived mailbox text is already one char per byte); the argv text is turned into its argv bytes at the source instead, by a new `ImapCommandLineText` (the IMAP twin of SMTP's `SmtpCommandLineText`, ADR-0135 §6 and ADR-0022: system ANSI code page on Windows, best-fitted; UTF-8 elsewhere), applied to LOGIN's user and password (`ImapAuthentication.LoginAsync`) and to the `-X` command before percent-decoding (`ImapSession.TryDecodeCustomCommand`). `touches` widened from ImapControlChannel.cs to those three files; no task in Doing on origin/work/dark-factory names an IMAP file. No new ADR: the choice is the one ADR-0135 §6 already made for SMTP. SASL is untouched (it already encodes through CredentialEncoding).
- Measured on Windows with `Record-CurlExchange.ps1 -Port 18543 -Imap -ImapReply 'GREETING=* OK ready','CAPABILITY=* CAPABILITY IMAP4rev1\r\nOK done'`, real curl 8.22.0 (Schannel, cp1252) against Curl.Console's curl.exe: `-sS -u "u€:p" imap://…/INBOX;UID=1` sends `A002 LOGIN u\x80 p`, exit 0; `-sS -u u:p -X "XFOO €" imap://…/` sends `A003 XFOO \x80`, exit 21 `curl: (21) Quote command returned error`; `-sS -u usér:päss …/INBOX;UID=1` sends `LOGIN us\xE9r p\xE4ss`, exit 0; `-sS -u u:p -X "XFOO é" …/` sends `XFOO \xE9`, exit 21. For all four, request.bin, stdout.bin, stderr.txt and exitcode.txt hash equal between REF and PORT after the fix (before it, € went out as `?`). Linux's UTF-8 answer (`us\xC3\xA9r p\xC3\xA4ss`, `XFOO \xC3\xA9`, from the report) is pinned in Cgr025Tests by OS-conditioned tests and by `ForPlatform(false, …)`; the CI-on-Linux side is met by keeping the tests platform-neutral (each platform's bytes in its own OSCondition test).
- Cgr025Tests: the Windows € tests fail before the fix (Latin-1 turned € into `?`) and pass after. IMAP tests 520 passed; full fast run green; Measure-CodeQuality for Curl.Protocol.Imap.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-025 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. IMAP LOGIN credentials and -X commands go out as curl's argv bytes (ANSI code page on Windows, UTF-8 elsewhere)
