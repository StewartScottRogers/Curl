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
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr086Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-086 of the third-party gap report.
