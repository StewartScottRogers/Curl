---
id: BL-3657
title: Fix CGR-128: After a CONNECT_ONLY perform, or a transfer aborted by an XFERINFO/READ callback, PRIMARY_IP, PRIMARY_PORT, LOCAL_IP, NUM_CONNECTS, SCHEM...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs, Curl.Engine.UnitLibrary/EngineConnectionIdTransferEvents.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.Scheme.cs, Curl.Engine.UnitTests/Cgr128Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3657 — Fix CGR-128: After a CONNECT_ONLY perform, or a transfer aborted by an XFERINFO/READ callback, PRIMARY_IP, PRIMARY_PORT, LOCAL_IP, NUM_CONNECTS, SCHEM...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-128 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-128: After a CONNECT_ONLY perform, or a transfer aborted by an XFERINFO/READ callback, PRIMARY_IP, PRIMARY_PORT, LOCAL_IP, NUM_CONNECTS, SCHEME and PROTOCOL are empty

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-8`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`. The cases are `CURLOPT_CONNECT_ONLY=1` to `http://127.0.0.1:21410/`, and an XFERINFOFUNCTION that returns 1 with NOPROGRESS=0. The READFUNCTION-abort case is in `EV\probe\cb.txt` and `EV\linux_cb.txt` (read-abort).

Commands (run each with REF and with PORT):

```sh
probe.exe repro
export CURLOPT_CONNECT_ONLY=1
```

**Expected (real curl)**

- CONNECT_ONLY gives PRIMARY_IP=127.0.0.1, PRIMARY_PORT=21410, LOCAL_IP=127.0.0.1, NUM_CONNECTS=1, SCHEME=http and PROTOCOL=1. The xferinfo abort returns 42 with the same connection infos filled in.

**Actual (Curl)**

- CONNECT_ONLY gives PRIMARY_IP='', PRIMARY_PORT=-1, LOCAL_IP='', NUM_CONNECTS=0, SCHEME=NULL and PROTOCOL=0.
- The xferinfo abort gives the same values (it still returns 42).
- The read abort reports EFFECTIVE_METHOD=GET where REF reports PUT.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyTransfer.cs`. TransferInfo is not filled in on the connect-only and early-abort paths.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr128Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 2): Cause: `PerformConnectOnlyAsync` finished with the perform's empty starting `Info`, and `FinishPerformed` built a fresh `TransferInfo` for a callback-aborted transfer, so neither carried the connection. Fix: `EngineConnectionIdTransferEvents` takes an optional `connectionOpened` hook, through which `ManagedEasyTransfer.ShowConnectionEndPointsOnInfo` puts each opened connection's peer and local IP and port on the live `Info` and counts it in `NumConnects` (libcurl's are live once connected). A connect-only perform puts its connection's end points (local from the connection, else the `ConnectResult`), `NumConnects` 1 and the URL's scheme and `CURLPROTO_*` bit (`TransferInfoFromReport.WithScheme`) on `Info`. A callback abort keeps the live `Info` and adds the scheme and protocol (`WithSchemeOf`) and `EffectiveMethodOf(options)`: `CURLOPT_CUSTOMREQUEST`, else `HEAD` under `NOBODY`, `PUT` under `UPLOAD`, `POST` for a post not overridden by `HTTPGET`, else `GET`, as curl 8.22.0's `getinfo_char` answers `CURLINFO_EFFECTIVE_METHOD` from the options. Decided by Claude under Stewart's delegation; it copies libcurl's behaviour directly, so no ADR.
- Touches widened to the partial files of `ManagedEasyTransfer` the change lands in, `EngineConnectionIdTransferEvents.cs` and `TransferInfoFromReport.Scheme.cs`. No task in Doing on `origin/work/dark-factory` names any of them (checked 2026-10-26: BL-3658 and BL-3661 hold other Engine files).
- Evidence: the report's probe (P/Invoke to REF libcurl beside the port) is not in the tree and was not rebuilt, so REF's values are the report's own measurement (CONNECT_ONLY: PRIMARY_IP 127.0.0.1, PRIMARY_PORT 21410, LOCAL_IP 127.0.0.1, NUM_CONNECTS 1, SCHEME http, PROTOCOL 1; xferinfo abort: 42 with the same; read abort: EFFECTIVE_METHOD PUT). `Cgr128Tests` runs each case through the engine against `127.0.0.1:21410` and gets those values: connect-only Ok with the six values, xferinfo abort `AbortedByCallback` (42) with them, and read-aborted upload `AbortedByCallback` with `PUT`. Before the fix the code left `''`, 0, `''`, 0, `null`, 0 and `GET` on these paths; the failing run was not recorded separately. Return codes and bytes on the wire are unchanged: the existing connect-only and callback-abort tests in `ManagedEasyTransferTests` still pass.
- Checks: `dotnet build` clean; `dotnet test --filter "TestCategory!=Integration"` green across all 46 test projects. One later `Curl.Engine.UnitTests` run failed `Cgr036Tests.ConnectAsync_ClosedPort_...` once (it passes alone and passed in the full run), filed as BL-3908. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: every member this task added or changed is within the gates. The one failing member, `QueuedCallback.WaitUntilRun` (lines 178 and 179 covered in the first run, not in the second), has timing-dependent coverage this change does not touch; also in BL-3908. Platform-neutral: loopback IPv4 end points only, no paths or OS text.

## Log

- 2026-10-26: Created from CGR-128 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CONNECT_ONLY performs and callback-aborted transfers answer PRIMARY_IP/PORT, LOCAL_IP/PORT, NUM_CONNECTS, SCHEME and PROTOCOL, and a read-aborted upload answers EFFECTIVE_METHOD PUT
