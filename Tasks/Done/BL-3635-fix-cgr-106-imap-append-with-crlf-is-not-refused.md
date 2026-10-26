---
id: BL-3635
title: Fix CGR-106: IMAP APPEND with --crlf is not refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapAppend.cs, Curl.Protocol.Imap.UnitLibrary/ImapSessionMessages.cs, Curl.Protocol.Imap.UnitTests/Cgr106Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3635 — Fix CGR-106: IMAP APPEND with --crlf is not refused

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-106 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-106: IMAP APPEND with `--crlf` is not refused

- **Severity:** Medium
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-7`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\confirm.cases` C12 and `EV\deep1.cases` u14; Linux in `EV/verify-linux.cases`. Command: `curl -s -S -u u:p --crlf -T EV\data\msg_lf.txt imap://127.0.0.1:21001/INBOX`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u u:p --crlf -T EV\data\msg_lf.txt imap://127.0.0.1:21001/INBOX
```

**Expected (real curl)**

- after authenticating it sends `A003 LOGOUT` and exits 25 with `curl: (25) Cannot APPEND with CRLF conversion making size unknown`.

**Actual (Curl)**

- sends `A003 APPEND INBOX (\Seen) {60}` followed by the LF-only data (no CRLF conversion either), then `A004 LOGOUT`, and exits 0.

**Suspected location in source**

- `Curl.Protocol.Imap.UnitLibrary\ImapAppend.cs`. It checks for a missing mailbox and an unknown size (`ImapSessionMessages.cs:78/81`) but has no `--crlf` check.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr106Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26 with Record-CurlExchange.ps1 -Imap (default replies), curl 8.22.0 Schannel (REF) and Curl.Console Debug build (PORT), 18-byte LF-only message: `-s -S -u u:p --crlf -T msg.txt imap://127.0.0.1:P/INBOX`, the same with `-T -` (stdin), and with `imap://127.0.0.1:P/` (no mailbox). REF, all three: CAPABILITY, AUTHENTICATE PLAIN, `A003 LOGOUT`, no APPEND, exit 25, stdout empty, stderr `curl: (25) Cannot APPEND with CRLF conversion making size unknown`. Before the fix PORT sent APPEND and exited 0 (the issue). After it PORT's request.bin and stderr.txt are byte-identical to REF's for all three, exit 25, stdout empty; no files written by either.
- Decision: the --crlf check runs first in ImapAppend.AppendAsync, before the missing-mailbox (exit 3) and unknown-size checks, because real curl gave the CRLF error for both of those variants too. No ADR: it only matches measured curl.
- Focused test: Cgr106Tests (three measured variants); the no-mailbox and -T - rows fail without the fix (exit 3 and the unknown-size text), and the INBOX row sent APPEND. Variants that already matched (uploads without --crlf) stay pinned by ImapProtocolHandlerAppendTests, all green.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary: 0 failing members. dotnet build clean, fast tests green on Windows; the change is platform-neutral (no paths, OS text or certificates in the test), which covers Linux and macOS.

## Log

- 2026-10-26: Created from CGR-106 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. IMAP upload with --crlf now logs out and exits 25 with curl's text, matching curl 8.22.0 byte for byte
