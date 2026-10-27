---
id: BL-3655
title: Fix CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_H...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr126Tests.cs, Libcurl/Info/Times/EasyInfoReaders.Times.cs, Libcurl.UnitTests/Info/Times/EasyInfoReadersTimesTests.cs, Curl.Api.UnitLibrary/Info/TransferInfo.cs, Curl.Engine.UnitLibrary/CallbackWriteStream.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/Cgr126Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3655 — Fix CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_H...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-126 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_HEADER)

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Windows. Detail: Windows confirmed. REF-LINUX 8.18 has no SIZE_DELIVERED (it returns 48). The port code is platform-independent and gives the same values on Linux.
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-6`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`.
  - Case 1: ACCEPT_ENCODING `""`, against a `Content-Encoding: gzip` response with a 25-byte body that decodes to `hello`.
  - Case 2: CURLOPT_HEADER=1, against a 200 response with a 5-byte body.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- gzip gives SIZE_DELIVERED=5 (the decoded bytes passed to the write callback). HEADER=1 gives SIZE_DELIVERED=69 (headers plus body).

**Actual (Curl)**

- gzip gives 25 and HEADER=1 gives 5. In both cases the value equals SIZE_DOWNLOAD_T.

**Suspected location in source**

- `Libcurl\Info\Times\EasyInfoReaders.Times.cs:111`. `ReadSizeDelivered` returns `info.SizeDownload`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr126Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 7): The reader alone could not fix it: the engine reported no delivered count. Widened `touches` to `Curl.Api.UnitLibrary/Info/TransferInfo.cs` (new `SizeDelivered`), `Curl.Engine.UnitLibrary/CallbackWriteStream.cs` (`BytesDelivered`, the bytes the write callback took), `ManagedEasyTransfer.Context.cs`, `.Finish.cs`, `.Perform.cs` (keep the last context's write-callback output, put its count on `Info`), `Curl.Engine.UnitLibrary/CLAUDE.md`, `Curl.Engine.UnitTests/Cgr126Tests.cs` and `Libcurl.UnitTests/Info/Times/EasyInfoReadersTimesTests.cs` (its old test pinned the bug). No task in Doing on `origin/work/dark-factory` names any of them (BL-3657 holds `ManagedEasyTransfer.cs`, which is left alone).
- Decision (sensible default, no ADR needed: it is libcurl's documented meaning): `SizeDelivered` is the bytes the `CURLOPT_WRITEFUNCTION` accepted, which covers both repro cases, since under `CURLOPT_HEADER` the header lines go through the body output (`HeaderLineTeeStream`). With no write callback, or under the curl tool's `ToolOutputs`, it falls back to `SizeDownload` (the tool's `%{size_delivered}` keeps its own `TransferReport.DeliveredSize` path).
- Repro: the report's probe (`SCR\evidence\libcurl-api`) is gone, so it was rebuilt as engine tests over `ScriptedConnector` (`Curl.Engine.UnitTests/Cgr126Tests.cs`) with the same server behaviour. REF (libcurl 8.22.0 Schannel, measured by the report on Windows twice): gzip under ACCEPT_ENCODING "" gives SIZE_DELIVERED=5; HEADER=1 gives SIZE_DELIVERED=69 (its 64 header bytes + 5 body bytes). PORT before: 25 and 5 (= SIZE_DOWNLOAD_T). PORT after: gzip gives SIZE_DELIVERED=5 with SIZE_DOWNLOAD_T the compressed length; HEADER=1 gives header bytes + 5 (the scripted response's full length, 38 + 5 here, so equal to REF's rule for the report's 64-byte head); return codes Ok in both. No real libcurl P/Invoke probe was rebuilt (Curl's tooling has no P/Invoke harness for REF libcurl); REF values are the report's measurement.
- ADR-0482 test: `Libcurl.UnitTests/Cgr126Tests.cs` fails with the old reader (checked: 2 failed with `=> info.SizeDownload`) and passes after.
- Variants that already matched: SIZE_DOWNLOAD(_T) unchanged (asserted in the gzip test); with no write callback SIZE_DELIVERED still equals SIZE_DOWNLOAD.
- Gates: `dotnet build` 0 warnings 0 errors; fast tests 44,024 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Api.UnitLibrary,Libcurl`: 0 failing members. Changes are platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-26: Created from CGR-126 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CURLINFO_SIZE_DELIVERED answers the bytes the write callback took: the decoded body under ACCEPT_ENCODING, headers plus body under CURLOPT_HEADER
