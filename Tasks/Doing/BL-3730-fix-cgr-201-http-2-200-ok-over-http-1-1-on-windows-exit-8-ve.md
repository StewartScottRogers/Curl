---
id: BL-3730
title: Fix CGR-201: "HTTP/2 200 OK" over HTTP/1.1 on Windows: exit 8 "Version mismatch" where REF (built without HTTP/2) exits 1
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs, Curl.Protocol.Http.UnitTests/Cgr201Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3730 — Fix CGR-201: "HTTP/2 200 OK" over HTTP/1.1 on Windows: exit 8 "Version mismatch" where REF (built without HTTP/2) exits 1

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-201 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-201: "HTTP/2 200 OK" over HTTP/1.1 on Windows: exit 8 "Version mismatch" where REF (built without HTTP/2) exits 1

- **Severity:** Low
- **Area:** http-responses
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0098, GF-0083
- **Source findings:** `http-responses HR-11`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- `st_v2.bin` = `HTTP/2 200 OK\r\nContent-Length: 2\r\n\r\nok`. Command: `curl -s -S http://127.0.0.1:20400/st_v2`

Commands (run each with REF and with PORT):

```sh
curl -s -S http://127.0.0.1:20400/st_v2
```

**Expected (real curl)**

- exit 1 `curl: (1) Unsupported HTTP version (2.0) in response`

**Actual (Curl)**

- exit 8 `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`. On Linux, REF-LINUX (nghttp2) also exits 8, so they match. PORT behaves like an HTTP/2-enabled build while its `-V` feature list claims no HTTP2.

**Suspected location in source**

- `HttpStatusLine.cs:107` (`RefusalOverHttp1`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr201Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-201 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
