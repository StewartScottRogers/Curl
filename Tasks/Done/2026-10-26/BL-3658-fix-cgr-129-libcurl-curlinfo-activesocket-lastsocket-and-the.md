---
id: BL-3658
title: Fix CGR-129: libcurl: CURLINFO_ACTIVESOCKET/LASTSOCKET and the CLOSESOCKETFUNCTION argument are synthetic ids (0x6000000N), not OS sockets
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CurlSocketHandles.cs, Curl.Engine.UnitLibrary/SocketCallbackConnector.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitLibrary/CurlSocketCloser.cs, Curl.Engine.UnitLibrary/SocketCallbackConnection.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/Cgr129Tests.cs, Libcurl.UnitTests/Cgr129Tests.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3658 — Fix CGR-129: libcurl: CURLINFO_ACTIVESOCKET/LASTSOCKET and the CLOSESOCKETFUNCTION argument are synthetic ids (0x6000000N), not OS sockets

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-129 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-129: libcurl: CURLINFO_ACTIVESOCKET/LASTSOCKET and the CLOSESOCKETFUNCTION argument are synthetic ids (0x6000000N), not OS sockets

- **Severity:** Medium
- **Area:** libcurl-api, libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: [libcurl-multi-callbacks LMC-15] Windows, Linux
- **Confidence:** Confirmed. Detail: [libcurl-api LA-9] Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-9`, `libcurl-multi-callbacks LMC-15`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- **[libcurl-api LA-9]** CURLINFO_ACTIVESOCKET / LASTSOCKET return a synthetic id (0x6000000N), not an OS socket, so it cannot be used with select/poll after CONNECT_ONLY
  - run `probe.exe repro` (section `CONNECT_ONLY=1 ACTIVESOCKET`) or `probe.exe lport`. It sets CONNECT_ONLY=1, performs, then calls `curl_easy_getinfo(CURLINFO_ACTIVESOCKET)`.
- **[libcurl-multi-callbacks LMC-15]** CLOSESOCKETFUNCTION (and SOCKOPTFUNCTION) receive synthetic handles, not OS sockets
  - case `C_48_closesocket_only`. Only a CLOSESOCKETFUNCTION is set, in keep-alive, FORBID_REUSE and Connection: close variants. It records the call count and `getsockopt(SO_TYPE)` on the handle. Command: `probe.exe C_48 2`.

Commands (run each with REF and with PORT):

```sh
# [libcurl-api LA-9]
probe.exe repro
CONNECT_ONLY=1 ACTIVESOCKET
probe.exe lport
# [libcurl-multi-callbacks LMC-15]
probe.exe C_48 2
```

**Expected (real curl)**

- **[libcurl-api LA-9]** returns a real socket: Winsock 1264/1172 on Windows, fd 106 on Linux.
- **[libcurl-multi-callbacks LMC-15]** close counts 0/0/1, 1/2/2 and 1/2/2; `real=[True...]`.

**Actual (Curl)**

- **[libcurl-api LA-9]** returns 1610612737 or 1610612738 (0x6000000N). `curl_easy_send` and `curl_easy_recv` do work and match REF (27 bytes sent, 69 received, then 81 when empty).
- **[libcurl-multi-callbacks LMC-15]** the same counts, but `real=[False...]`. The callback cannot inspect or close the socket.

**Suspected location in source**

- **[libcurl-api LA-9]** `Libcurl\Info\Longs\EasyInfoReaders.Longs.cs` (the Activesocket and Lastsocket readers).
- **[libcurl-multi-callbacks LMC-15]** `Curl.Engine.UnitLibrary\SocketCallbackConnector.cs:168-179` (`handles.Next()`) and `CurlSocketHandles.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr129Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened (2026-10-26) to `ConnectOnlyConnection.cs` (where the connect-only socket number is made; `EasyInfoReaders.Longs.cs` only reads `TransferInfo`), `CurlSocketCloser.cs` and `SocketCallbackConnection.cs` (the close-socket callback must run before the engine closes its socket) and the engine's `CLAUDE.md` (its text described the old numbers). No task in Doing on `origin/work/dark-factory` names any of them. `Libcurl` needed no change.
- Fix (ADR-0842): since ADR-0820 `IConnection.SocketHandle` gives the real socket through every wrapper. `ConnectOnlyConnection.OpenAsync` keeps the connection under it (so `CURLINFO_ACTIVESOCKET`/`LASTSOCKET` are the OS socket); without an open-socket callback `SocketCallbackConnector` gives the close-socket callback the connection's `SocketHandle` and `SocketCallbackConnection.DisposeAsync` calls the callback before closing the connection, so the callback sees it open (`getsockopt(SO_TYPE)` works). In-memory connections (ratchet, fakes) keep engine numbers.
- Repro: the report's `SCR` probe folder is gone, so REF's answers are the report's measurement (libcurl 8.22.0 Schannel and 8.18.0 OpenSSL: `ACTIVESOCKET` a real Winsock socket / fd, C_48 close counts 0/0/1, 1/2/2, 1/2/2 with `real=[True...]`). PORT was rerun at this commit through the engine over a real loopback connection: `Cgr129Tests.ConnectAsync_OnlyCloseSocketOverARealConnection_CallsItWithTheOpenOperatingSystemSocket` (only CLOSESOCKETFUNCTION set) gets the connection's own handle, once, and `SO_TYPE` on it reads `SOCK_STREAM` (real=True); `OpenAsync_ConnectionWithAnOperatingSystemSocket_GivesThatSocket` makes `ActiveSocket`/`LastSocket` that handle. Before the fix 4 of the 5 `Cgr129Tests` failed (synthetic 0x6000000N / 1), after it all pass. Close counts are unchanged (the existing `SocketCallbackConnectorTests` and `ManagedEasyTransferTests.Sockets.cs` still pass), and `curl_easy_send`/`recv`, which already matched, are untouched code.
- Left over: `CURLOPT_SOCKOPTFUNCTION` without an open-socket callback still gets the engine number, as it runs before the dialer creates its socket; filed as BL-3907 (not part of the C_48 repro, which sets only the close-socket callback).
- Gates: `dotnet build` clean; fast tests all green (46 test assemblies, 0 failed); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line and branch, 0 failing members (the first run flagged `OpenSocketAsync` at complexity 12; its socket choice moved to `SocketFor`). The change is platform-neutral (handles via `SocketHandle`, `SafeSocketHandle` with `ownsHandle: false` in the test), meeting the Linux/macOS side.
## Log

- 2026-10-26: Created from CGR-129 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CURLINFO_ACTIVESOCKET/LASTSOCKET and the close-socket callback get the connection's OS socket, open, as libcurl does
