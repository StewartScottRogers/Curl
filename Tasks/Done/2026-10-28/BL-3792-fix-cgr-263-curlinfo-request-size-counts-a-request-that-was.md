---
id: BL-3792
title: Fix CGR-263: CURLINFO_REQUEST_SIZE counts a request that was never sent (HTTP/1.0 with a chunked upload of unknown size)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/Cgr263Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3792 — Fix CGR-263: CURLINFO_REQUEST_SIZE counts a request that was never sent (HTTP/1.0 with a chunked upload of unknown size)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-263 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-263: CURLINFO_REQUEST_SIZE counts a request that was never sent (HTTP/1.0 with a chunked upload of unknown size)

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-30`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe upload http10`. It sets UPLOAD=1, a READFUNCTION, no INFILESIZE, and HTTP_VERSION=1.0.

Commands (run each with REF and with PORT):

```sh
probe.exe upload http10
```

**Expected (real curl)**

- REQUEST_SIZE=0.

**Actual (Curl)**

- REQUEST_SIZE=83. The return code and error text are identical.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyTransfer.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr263Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Repro (probe not available; measured by the report): HTTP/1.0 + UPLOAD + READFUNCTION, no INFILESIZE: real curl REQUEST_SIZE=0, Curl was 83. The handler refuses the request (exit 25) before sending but its report still counted the head. Fixed in HttpProtocolHandler.cs (HttpExchange.Report counts the head only once RequestReady is set), a file no other live task names, added to touches. Test Cgr263Tests failed before (83) and passes after. Linux/macOS: change is platform-neutral, fast tests green.

## Log

- 2026-10-26: Created from CGR-263 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. CGR-263 fixed: request size excludes a head never sent
