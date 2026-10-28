---
id: BL-3768
title: Fix CGR-239: SMTP/IMAP: when the next URL needs a different login, PORT sends QUIT/LOGOUT on the first connection at once; curl keeps it open until exit
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapSession.cs, Curl.Protocol.Imap.UnitTests/Cgr239Tests.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpControlChannel.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpKeptConnection.cs, Curl.Protocol.Smtp.UnitTests/Cgr239Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3768 — Fix CGR-239: SMTP/IMAP: when the next URL needs a different login, PORT sends QUIT/LOGOUT on the first connection at once; curl keeps it open until exit

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-239 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-239: SMTP/IMAP: when the next URL needs a different login, PORT sends QUIT/LOGOUT on the first connection at once; curl keeps it open until exit

- **Severity:** Low
- **Area:** upstream-cases
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** GF-0057, GF-0051
- **Source findings:** `upstream-cases UC-9`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- `verify\v2.sh`, confs `verify\conf\smtp938.json` and `imap836.json` (parallel line server). Commands:
  - `curl smtp://127.0.0.1:21355/938001 --mail-rcpt recipient@example.com --mail-from sender@example.com -u user.one:secret -T upload -: smtp://127.0.0.1:21355/938002 --mail-rcpt recipient@example.com --mail-from sender@example.com -u user.two:secret -T upload`
  - `curl "imap://127.0.0.1:21356/836/;MAILINDEX=1" -u user.one:secret -: "imap://127.0.0.1:21356/836/;UID=2" -u user.two:secret`

Commands (run each with REF and with PORT):

```sh
curl smtp://127.0.0.1:21355/938001 --mail-rcpt recipient@example.com --mail-from sender@example.com -u user.one:secret -T upload -: smtp://127.0.0.1:21355/938002 --mail-rcpt recipient@example.com --mail-from sender@example.com -u user.two:secret -T upload
curl "imap://127.0.0.1:21356/836/;MAILINDEX=1" -u user.one:secret -: "imap://127.0.0.1:21356/836/;UID=2" -u user.two:secret
```

**Expected (real curl)**

- The first connection's QUIT (SMTP) or A005 LOGOUT (IMAP) is sent at the end, after the second connection's session.

**Actual (Curl)**

- QUIT / A005 LOGOUT is sent on the first connection straight after its transfer, before the second session starts. Exit codes and auth bytes match.

**Suspected location in source**

- `Curl.Protocol.Smtp.UnitLibrary\SmtpControlChannel.cs:221`, `SmtpKeptConnection.cs`; `Curl.Protocol.Imap.UnitLibrary\ImapSession.cs:895`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr239Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-239 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
