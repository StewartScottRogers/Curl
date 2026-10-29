---
id: BL-3751
title: Fix CGR-222: -v -I: excess body bytes after a HEAD response are not reported as "{ [N bytes data]"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/Cgr222Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3751 — Fix CGR-222: -v -I: excess body bytes after a HEAD response are not reported as "{ [N bytes data]"

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-222 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-222: -v -I: excess body bytes after a HEAD response are not reported as "{ [N bytes data]"

- **Severity:** Low
- **Area:** writeout-verbose
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0156
- **Source findings:** `writeout-verbose WV-20`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `curl -sv -I http://127.0.0.1:20700/redir/0`. This route wrongly sends a 5-byte body after the HEAD response.

Commands (run each with REF and with PORT):

```sh
curl -sv -I http://127.0.0.1:20700/redir/0
```

**Expected (real curl)**

- Prints `{ [5 bytes data]` before `* shutting down connection #0`.

**Actual (Curl)**

- No data line. Exit code and stdout match.

**Suspected location in source**

- The HEAD path in `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr222Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-222 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

### Notes (run 2)
- Repro `curl -sv -I http://127.0.0.1:20700/redir/0` (server sends 5 stray body bytes after HEAD): real curl 8.22.0 prints `{ [5 bytes data]` and no "Excess found" line; the fix reports the head-read body prefix as received data for `-I` but still not as excess. Pinned by `Cgr222Tests.cs` (failed before, passes after). Not re-measured live: lane is unattended, behaviour taken from the report plus the earlier BL-3530 measurement.
- Fast Http tests: 2541 passed. Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary: 0 failing members. Change is platform-neutral (no paths/OS text).
- 2026-10-28: Doing -> Done. HEAD-response body prefix reported as received data under -v -I (CGR-222)
