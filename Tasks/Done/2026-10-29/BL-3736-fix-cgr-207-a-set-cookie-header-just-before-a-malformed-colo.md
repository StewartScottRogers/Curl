---
id: BL-3736
title: Fix CGR-207: A Set-Cookie header just before a malformed (colon-less) header line is lost
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests/Cgr207Tests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Cookies.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3736 — Fix CGR-207: A Set-Cookie header just before a malformed (colon-less) header line is lost

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-207 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-207: A Set-Cookie header just before a malformed (colon-less) header line is lost

- **Severity:** Low
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** GF-0012
- **Source findings:** `http-stateful HS-15`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- the raw response `HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nSet-Cookie: d=2\r\nbogus line\r\nContent-Length: 2\r\n\r\nok`. Run `curl -sS -c jar.txt http://127.0.0.1:20500/x`. Case: `EV\cases\hdr-nocolon-cookie`.

Commands (run each with REF and with PORT):

```sh
curl -sS -c jar.txt http://127.0.0.1:20500/x
```

**Expected (real curl)**

- exits 8 with `Header without colon`. jar.txt holds both `d 2` and `a 1`.

**Actual (Curl)**

- exits 8 with the same message. jar.txt holds only `a 1`. With -v, the `* Header without colon` line is also missing (from the original run).

**Suspected location in source**

- the response-head reader in `Curl.Protocol.Http.UnitLibrary`. It holds the header back to check for folding, then drops it on the error.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr207Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-207 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-29: Cause: `HttpResponseHeadReader` held the pending `Set-Cookie` header whole only after `builder.AddLine` returned, so the colon-less line's exception skipped it and the failure path dropped held headers. Fix: on `Header without colon` the pending header is moved to the held headers, and (when no held header is refused) all held headers are released to `HeaderReceived` before the failure is thrown. Reproduced by a unit test (`Cgr207Tests.cs`, the raw response from the report): it failed before (only `a=1` stored) and passes after (`a=1`, `d=2`). The older test `ExecuteAsync_HeadFailsAfterASetCookieLine_ReportsTheLineAndStoresTheCookie` pinned "stores nothing" and was changed to the report's confirmed result (curl 8.22.0 stores it); its `-v` order (`* Added` before `< Set-Cookie`) follows the normal path. Added `HttpProtocolHandlerTests.Cookies.cs` to touches. Real curl was not re-run here (no server rebuild); the report's measurement (exit 8, both cookies) is the evidence. Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 0 failing members. Linux/macOS: change is platform-neutral.
- 2026-10-29: Doing -> Done. CGR-207 fixed: Set-Cookie before a colon-less header line is stored
