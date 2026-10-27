---
id: BL-3651
title: Fix CGR-122: CURLOPT_NOBODY=1 set after CURLOPT_UPLOAD=1 still sends a PUT with a body (libcurl switches to HEAD)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr122Tests.cs, Libcurl/Options/Http/EasyHttpOptionSetters.cs, Libcurl/Options/Transfer/EasyTransferOptionSetters.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3651 — Fix CGR-122: CURLOPT_NOBODY=1 set after CURLOPT_UPLOAD=1 still sends a PUT with a body (libcurl switches to HEAD)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-122 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-122: CURLOPT_NOBODY=1 set after CURLOPT_UPLOAD=1 still sends a PUT with a body (libcurl switches to HEAD)

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-2`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` (section `UPLOAD=1 then NOBODY=1`). It sets URL `http://127.0.0.1:21410/f`, UPLOAD=1, INFILESIZE_LARGE=15 and a READFUNCTION, then NOBODY=1, then performs.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
UPLOAD=1 then NOBODY=1
```

**Expected (real curl)**

- sends `HEAD /f HTTP/1.1` with no body. EFFECTIVE_METHOD=HEAD.

**Actual (Curl)**

- sends `PUT /f HTTP/1.1` with `Content-Length: 15` and the body. EFFECTIVE_METHOD=PUT.

**Suspected location in source**

- `Libcurl\Options\Http\EasyHttpOptionSetters.cs`. The NOBODY setter does not override the upload method the way setopt.c does.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr122Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Fix: the NOBODY setter is in `Libcurl/Options/Transfer/EasyTransferOptionSetters.cs`, not `EasyHttpOptionSetters.cs`, so that file was added to `touches` (no other task in Doing on origin/work/dark-factory names it). NOBODY=1 now clears `Upload`: setopt.c sets the method to `HTTPREQ_HEAD`, and `Curl_pretransfer` then finds no upload. NOBODY=0 leaves an upload as it is, since setopt.c only turns HEAD back into GET. No ADR, as the fix follows setopt.c directly.
- Repro: a C# file-based probe in %TEMP% (not committed) with a loopback server, calling `C:\Program Files\Git\ucrt64\bin\libcurl-4.dll` (REF) through P/Invoke and the port's Libcurl (PORT) in one process. It sets URL `http://127.0.0.1:<port>/f`, UPLOAD=1, INFILESIZE_LARGE=15 and a READFUNCTION, then NOBODY=1, then performs. REF: rc=0, EFFECTIVE_METHOD=HEAD, wire `HEAD /f HTTP/1.1`, `Host: 127.0.0.1:<port>`, `Accept: */*` and no body. PORT after the fix: rc=Ok, EFFECTIVE_METHOD=HEAD, the same bytes on the wire. Before the fix the port sent PUT with `Content-Length: 15` and the body (the report).
- `Cgr122Tests.SetOption_NobodyOnAfterUpload_ClearsTheUploadAndSetsNoBody` fails before the fix: the old setter changed only `NoBody`, so `Upload` stayed true. It passes after the fix. `SetOption_NobodyZeroAfterUpload_KeepsTheUpload` pins the case that already behaved like curl. The issue has no other repro variants.
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. `dotnet build` is clean with 0 warnings, and the fast tests pass (Libcurl.UnitTests: 1431 passed, 1 skipped). The change is pure option logic and works the same on every platform, so the Linux and macOS runs are safe.

## Log

- 2026-10-26: Created from CGR-122 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CURLOPT_NOBODY=1 set after CURLOPT_UPLOAD=1 now sends a HEAD with no body, as libcurl does
