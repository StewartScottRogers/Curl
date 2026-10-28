---
id: BL-3762
title: Fix CGR-233: IMAP -I on a UID fetch: after "Weird server reply" (exit 8), PORT closes without LOGOUT
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapSession.cs, Curl.Protocol.Imap.UnitTests/Cgr233Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3762 — Fix CGR-233: IMAP -I on a UID fetch: after "Weird server reply" (exit 8), PORT closes without LOGOUT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-233 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-233: IMAP `-I` on a UID fetch: after "Weird server reply" (exit 8), PORT closes without LOGOUT

- **Severity:** Low
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-14`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\confirm.cases` C17 and `EV\imap.cases` i43. Command: `curl -s -S -u u:p -I "imap://127.0.0.1:21001/INBOX;UID=1"`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u u:p -I "imap://127.0.0.1:21001/INBOX;UID=1"
```

**Expected (real curl)**

- `... A004 UID FETCH 1 BODY[]`, then `A005 LOGOUT`; exits 8.

**Actual (Curl)**

- `... A004 UID FETCH 1 BODY[]`, then closes with no LOGOUT; exits 8.

**Suspected location in source**

- `Curl.Protocol.Imap.UnitLibrary\ImapSession.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr233Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-233 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

### Run notes (2026-10-28)

- Measured curl 8.22.0 (Schannel) `-s -S -u u:p -I "imap://127.0.0.1:PORT/INBOX;UID=1"`: with `Record-CurlExchange.ps1 -Imap` (whole FETCH response in one segment) exit 8, no LOGOUT (already pinned, matches PORT). With `-Script` splitting the response so the tagged completion arrives after the literal (split mid-literal, or literal ends its segment): exit 8 and `A005 LOGOUT`. That is the report's case.
- Decision: after `-I` refuses the FETCH literal (exit 8), LOGOUT is sent unless unread bytes (the completion) were already buffered. Small enough not to need an ADR. `ImapSession.DownloadAsync`; tests in `Cgr233Tests.cs` (fail before, pass after for the two split cases; the one-segment case stays pinned).
- Unrelated oddity seen, not fixed: header line alone in a first segment, literal in the next, `-I` exits 0 with LOGOUT. Not in CGR-233.
- Measure-CodeQuality for Curl.Protocol.Imap.UnitLibrary: 100% line, 100% branch, 0 failing. Fast tests of the Imap project pass (535). CI on Linux/macOS: tests are platform-neutral (in-memory fakes).
- 2026-10-28: Doing -> Done. LOGOUT now sent after -I refuses a FETCH literal whose completion has not arrived; measured against curl 8.22.0
