---
id: BL-3615
title: Fix CGR-086: -L to a Location that cannot be parsed exits 0 silently (curl exits 3 or 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr086Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3615 — Fix CGR-086: -L to a Location that cannot be parsed exits 0 silently (curl exits 3 or 1)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-086 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-086: -L to a Location that cannot be parsed exits 0 silently (curl exits 3 or 1)

- **Severity:** Medium (wrong exit code, but only for malformed or unsupported redirect targets)
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** AF-0021, GF-0008
- **Source findings:** `writeout-verbose WV-04`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `resp/badloc.txt` is `HTTP/1.1 301 Moved` with `Location: http://[bad`. `resp/protoloc.txt` has `Location: foo://x/`.
- `curl -sS -L -w '%{exitcode}|%{errormsg}|%{num_redirects}\n' http://127.0.0.1:20700/raw/badloc`
- The same command against `/raw/protoloc`.

Commands (run each with REF and with PORT):

```sh
curl -sS -L -w '%{exitcode}|%{errormsg}|%{num_redirects}\n' http://127.0.0.1:20700/raw/badloc
```

**Expected (real curl)**

- `badloc`: `3|The redirect target URL could not be parsed: Bad IPv6 address|1`, plus `curl: (3) ...` on stderr.
- `protoloc`: `1|...Unsupported URL scheme|1`, exit 1.

**Actual (Curl)**

- `0||0`, exit 0, nothing on stderr. The 301 is treated as the final response.

**Suspected location in source**

- `Curl.Core.UnitLibrary/RedirectFollower.cs`, the StopBeforeHop refusal path.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr086Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 3): Not reproducible at 246f02ff0. Measured with `Record-CurlExchange.ps1` (real curl 8.22.0 Schannel at /ucrt64/bin/curl, then `-Curl` set to Curl.Console's curl.exe), command `curl -sS -L -w '%{exitcode}|%{errormsg}|%{num_redirects}\n' http://127.0.0.1:<port>/raw/x`, the server answering `HTTP/1.1 301 Moved` with `Location: http://[bad` or `Location: foo://x/`. Both give, byte for byte: `http://[bad` -> exit 3, stdout `3|The redirect target URL could not be parsed: Bad IPv6 address|1`, stderr `curl: (3) The redirect target URL could not be parsed: Bad IPv6 address`; `foo://x/` -> exit 1, stdout `1|The redirect target URL could not be parsed: Unsupported URL scheme|1`, stderr `curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme`. No files written. Request bytes are the same (`GET /raw/x HTTP/1.1`, Host, `User-Agent: curl/8.22.0`, `Accept: */*`) apart from the port. Same result for the response variants tried: Content-Length: 0, no Content-Length, `Connection: close` with a body, a 5-byte body, and LF-only line endings.
- Focused test criterion: there was no defect left to fix, so `Curl.Core.UnitTests/Cgr086Tests.cs` pins both repros at RedirectFollower as a regression guard (exit, message, num_redirects 1, one request). It also passes with b0d27d68's `RedirectFollower.cs` swapped in, so the report's `0||0` did not come from this file at b0d27d68; it was not traced further. Ticked on that evidence, as these Notes allow for an issue that does not reproduce.
- Build clean (0 warnings); fast tests: exit 0, 43672 passed, 290 skipped. No production code changed, so no Measure-CodeQuality run was needed. The test is platform-neutral (no paths, no OS text), so CI on Linux and macOS is covered.

## Log

- 2026-10-26: Created from CGR-086 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CGR-086 does not reproduce: -L to an unparsable or unknown-scheme Location exits 3/1 with curl's message and output; regression test added
