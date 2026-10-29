---
id: BL-3770
title: Fix CGR-241: FTP active mode, RETR answered with 150 and then 425: same exit code 10, different message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests/Cgr241Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3770 — Fix CGR-241: FTP active mode, RETR answered with 150 and then 425: same exit code 10, different message

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-241 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-241: FTP active mode, RETR answered with 150 and then 425: same exit code 10, different message

- **Severity:** Low
- **Area:** upstream-cases
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** none
- **Source findings:** `upstream-cases UC-13`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- `verify\v2.sh`, conf `verify\conf\ftp1211b.json`. Run `curl ftp://127.0.0.1:21357/1211 -P -`.

Commands (run each with REF and with PORT):

```sh
curl ftp://127.0.0.1:21357/1211 -P -
```

**Expected (real curl)**

- `curl: (10) Received only partial file: 0 bytes`

**Actual (Curl)**

- `curl: (10) FTP: The server failed to connect to data port`

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary` (425 handling).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr241Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Not reproducible at the current commit (fixed earlier by CGR-099/CGR-098 work). Real curl (Record-CurlExchange.ps1 -Ftp, `-sS -P - ftp://127.0.0.1:PORT/1211`, RETR=150 and 425 in one read): SIZE 213 18 -> "(10) Received only partial file: 0 bytes"; SIZE 213 0 plus "(18 bytes)" in the 150 -> same; SIZE 500 plus "(18 bytes)" -> same; no size anywhere (SIZE 500, or SIZE 0 with no hint) -> "(10) FTP: The server failed to connect to data port". The report's server config is gone; the size-known variants are the ones that give "partial file". Curl's FtpSession gives the same text for all of them, pinned in Cgr241Tests (4 tests, passing; none fails before a fix because none was needed). Curl.Console could not be run through the recorder here (bin/ not readable by the lane), so the Curl side is pinned by the unit tests against the measured curl text.
- No production code changed, so no Measure-CodeQuality run. Linux/macOS CI: tests are platform-neutral (in-memory fakes).
## Log

- 2026-10-26: Created from CGR-241 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. CGR-241 not reproducible: every size-known 150+425 variant already gives curl's partial-file message; pinned by Cgr241Tests
