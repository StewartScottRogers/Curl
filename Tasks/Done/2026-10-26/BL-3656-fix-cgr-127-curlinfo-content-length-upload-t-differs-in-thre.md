---
id: BL-3656
title: Fix CGR-127: CURLINFO_CONTENT_LENGTH_UPLOAD(_T) differs in three cases: -1 for body-less requests (REF 0), the POST size kept after a POST-to-GET redi...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr127Tests.cs, Libcurl/Info/Times/EasyInfoReaders.Times.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/Cgr127Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3656 — Fix CGR-127: CURLINFO_CONTENT_LENGTH_UPLOAD(_T) differs in three cases: -1 for body-less requests (REF 0), the POST size kept after a POST-to-GET redi...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-127 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-127: CURLINFO_CONTENT_LENGTH_UPLOAD(_T) differs in three cases: -1 for body-less requests (REF 0), the POST size kept after a POST-to-GET redirect (REF 0), and a size given for chunked uploads (REF -1)

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-7`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`.
  - Case a: a plain GET to `http://127.0.0.1:21410/`.
  - Case b: COPYPOSTFIELDS `q=1` with FOLLOWLOCATION=1, against a 301 to `/n`.
  - Case c: COPYPOSTFIELDS `abc` with HTTPHEADER `Transfer-Encoding: chunked`.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- (a) 0, (b) 0, (c) -1.

**Actual (Curl)**

- (a) -1, (b) 3, (c) 3.

**Suspected location in source**

- `Libcurl\Info\Times\EasyInfoReaders.Times.cs:127` (from the engine's `TransferInfo.ContentLengthUpload`).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr127Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 7): The value is computed in the engine, not the Libcurl reader (`EasyInfoReaders.Times.cs` just passes `TransferInfo.ContentLengthUpload` through, which is right), so the fix is `ManagedEasyTransfer.ContentLengthUploadOf(EasyOptions, TransferReport?, string)` in `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs`, and the focused test is `Curl.Engine.UnitTests/Cgr127Tests.cs` (the engine's own tests, ADR-0482). Added those files and `Curl.Engine.UnitLibrary/CLAUDE.md` to `touches`; no task in Doing on origin/work/dark-factory names them (BL-3657 holds `ManagedEasyTransfer.cs` only). `Libcurl.UnitTests/Cgr127Tests.cs` was not needed: nothing in Libcurl changed. Decision recorded in ADR-0843.
- Measurement: the report's probe (P/Invoke into Git's `libcurl-4.dll`) is gone with its scratch folder and is a libcurl-API probe that `Record-CurlExchange.ps1` (a curl command-line recorder) cannot drive; the curl tool has no `-w` variable for this info. The expected values are the report's own measurements of libcurl 8.22.0 Schannel and 8.18.0 OpenSSL (both: a 0, b 0, c -1), which match libcurl's source (`Curl_pgrsSetUploadSize` from each request's client reader). Curl before the fix: a -1, b 3, c 3 (the old options-only answer); after: a 0, b 0, c -1, pinned end to end over a scripted HTTP server by `Cgr127Tests` (`PerformAsync_PlainGet_ContentLengthUploadIsZero`, `..._PostRedirectedTo301Get_...IsZero`, `..._PostWithChunkedTransferEncodingHeader_...IsUnknown`). A post with a `Content-Length` still answers 3 and uploads, form posts, non-HTTP schemes and transfers that sent no request keep the earlier answers (the existing `ContentLengthUploadOf_Options_*` rows and `PerformAsync_Upload*` test unchanged and green), so the variants that matched still match. Exit codes, stdout and wire bytes do not change: only the info value does.
- Platform: the tests use scripted connectors and `http://h/` URLs only, so they do not depend on the platform.
- `dotnet build` clean, `dotnet test --filter "TestCategory!=Integration"` green across the solution; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 0 failing members (after splitting two new helpers the first run put over the complexity gate).

## Log

- 2026-10-26: Created from CGR-127 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CURLINFO_CONTENT_LENGTH_UPLOAD answers 0, 0 and -1 for the three CGR-127 cases, pinned by Cgr127Tests; build clean, fast tests green, quality gate clean
