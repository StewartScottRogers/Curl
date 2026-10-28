---
id: BL-3724
title: Fix CGR-195: Redirect target parse errors give different reason texts (Bad hostname / port-number text instead of Unsupported number of slashes follow...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr195Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3724 — Fix CGR-195: Redirect target parse errors give different reason texts (Bad hostname / port-number text instead of Unsupported number of slashes follow...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-195 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-195: Redirect target parse errors give different reason texts (`Bad hostname` / port-number text instead of `Unsupported number of slashes following scheme`, `No host present`, `Bad file:// URL`)

- **Severity:** Low
- **Area:** url-parsing, upstream-cases
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-14] Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-21`, `upstream-cases UC-14`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- **[url-parsing UP-21]** Redirect target parse errors give different reason texts
  - `curl -sS -L "http://127.0.0.1:20200/__loc=http:x"` (v21_redir_errtext)
  - `curl -sS -L "http://127.0.0.1:20200/__loc=//"` (v21b_redir_errtext2)
  - `curl -sS -L "http://127.0.0.1:20200/__loc=file://localhost:1/y"` (v21c_redir_file_err)
- **[upstream-cases UC-14]** Redirect to `http:////` fails with a different message (exit 3 on both)
  - `verify\v2.sh`, server `verify\conf\r1142.json` replies `HTTP/1.1 302 OK\r\nLocation: http:////\r\nContent-Length: 0\r\n\r\n`. Run `curl -s -S -L http://127.0.0.1:21358/x`.

Commands (run each with REF and with PORT):

```sh
# [url-parsing UP-21]
curl -sS -L "http://127.0.0.1:20200/__loc=http:x"
curl -sS -L "http://127.0.0.1:20200/__loc=//"
curl -sS -L "http://127.0.0.1:20200/__loc=file://localhost:1/y"
# [upstream-cases UC-14]
curl -s -S -L http://127.0.0.1:21358/x
```

**Expected (real curl)**

- **[url-parsing UP-21]** the reasons are `Unsupported number of slashes following scheme`, `No host present` (Linux 8.18: `No host part in the URL`) and `Bad file:// URL`.
- **[upstream-cases UC-14]** `curl: (3) The redirect target URL could not be parsed: Unsupported number of slashes following scheme`

**Actual (Curl)**

- **[url-parsing UP-21]** the reasons are `Port number was not a decimal number between 0 and 65535`, `Bad hostname` and `Bad hostname`. The exit code is 3 for both.
- **[upstream-cases UC-14]** `curl: (3) The redirect target URL could not be parsed: Bad hostname`

**Suspected location in source**

- **[url-parsing UP-21]** `Curl.Core.UnitLibrary/RedirectFollower.cs:403` (UnparsableUrlReason).
- **[upstream-cases UC-14]** the port's URL parser (redirect target validation).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes
- Measured curl 8.22.0 Schannel (`-sS -L`, Location target; PowerShell loopback server): `http:x`, `http:////` -> exit 3 `Unsupported number of slashes following scheme`; `//` (=`http://`) and `http://:80/` -> `No host present`; `file://localhost:1/y` -> `Bad file:// URL`; `http://h:99999/` -> port text. Curl now prints identical lines for all (verified with Curl.Console). Linux curl 8.18 says `No host part in the URL` for the empty host; the Windows text is pinned (decision: match the Schannel build measured here).
- Fix in `RedirectFollower.UnparsableUrlReason` (split into `AuthorityReason`); test `Cgr195Tests` failed before (4 rows) and passes. Core fast tests 1930 pass; Measure-CodeQuality for Curl.Core.UnitLibrary: 0 failing members. CI Linux/macOS criterion met by platform-neutral tests.

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr195Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-195 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. Redirect target parse errors give curl's slashes / no host / bad file reasons
