---
id: BL-3674
title: Fix CGR-145: SOCKOPTFUNCTION runs after the connect with a fake socket handle; CURL_SOCKOPT_ERROR still connects, and CURL_SOCKOPT_ALREADY_CONNECTED i...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CurlSocketHandles.cs, Curl.Engine.UnitLibrary/SocketCallbackConnector.cs, Curl.Engine.UnitTests/Cgr145Tests.cs, Curl.Engine.UnitLibrary/CurlSocketCloser.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/Cgr036Tests.cs, Curl.Networking.UnitLibrary/DeferredConnectSocketStream.cs, Curl.Networking.UnitTests/DeferredConnectSocketStreamTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3674 — Fix CGR-145: SOCKOPTFUNCTION runs after the connect with a fake socket handle; CURL_SOCKOPT_ERROR still connects, and CURL_SOCKOPT_ALREADY_CONNECTED i...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-145 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-145: SOCKOPTFUNCTION runs after the connect with a fake socket handle; CURL_SOCKOPT_ERROR still connects, and CURL_SOCKOPT_ALREADY_CONNECTED is ignored

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-6`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_43_sockopt`. URL `http://127.0.0.1:21700/`, CONNECTTIMEOUT_MS 2000, no OPENSOCKETFUNCTION. SOCKOPTFUNCTION records `getsockopt(SO_TYPE)` on the handle it gets, and returns OK, then ERROR, then ALREADY_CONNECTED. Command: `probe.exe C_43 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_43 2
```

**Expected (real curl)**

- *REF (Windows 8.22):* `Ok: rc=0 real=True`; `Error: rc=42 serverconns=0`; `AlreadyConnected: rc=55 reqs=0 serverconns=0`. Linux 8.18 also returns rc=55, but its server records one connection.

**Actual (Curl)**

- `real=False` on every call, so setsockopt on the handle fails. `Error: rc=42 serverconns=1`; `AlreadyConnected: rc=0 reqs=1`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\SocketCallbackConnector.cs:168-185` (`handles.Next()`, called after the inner connect) and `CurlSocketHandles.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr145Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Plan and decision (ADR-0845): without an open-socket callback, a set socket-option callback over a dialer that adopts sockets gets a TCP socket `AttemptSocketOpener` opens itself (with `TCP_NODELAY`) before the dial; it goes to the dialer as `Supplied`, unconnected under `AlreadyConnected`, and its `CurlSocketCloser` owns and closes it after the close-socket callback. Only the socket-option callback triggers it, so a close-socket-only handle keeps the dialer's own socket and options (ADR-0842).
- Touches widened, since no task in Doing on origin/work/dark-factory names these files: `CurlSocketCloser.cs` (it owns the engine socket), `Cgr036Tests.cs` (one test pinned the fake handle `1` for the error case), `Curl.Engine.UnitLibrary/CLAUDE.md` (the socket callbacks paragraph), and `Curl.Networking.UnitLibrary/DeferredConnectSocketStream.cs` plus its tests. That stream let a raw `SocketException` out of `WriteAsync` on the unconnected socket, which escaped the perform, so it now wraps it in an `IOException` as `NetworkStream` does, and the HTTP handler answers exit 55.
- Repro: a C# file-based probe outside the repository (`dotnet run probe.cs -- REF|PORT`), with a loopback server answering `200 hello`, CONNECTTIMEOUT_MS 2000, and SOCKOPTFUNCTION checking `SO_TYPE` on its handle and returning OK, then ERROR, then ALREADY_CONNECTED. REF is Git for Windows' `libcurl-4.dll` 8.22.0 Schannel; PORT is `ManagedCurlEngine`.
  - REF: `Ok: rc=0 real=True reqs=1 serverconns=1`; `Error: rc=42 real=True reqs=0 serverconns=0`; `AlreadyConnected: rc=55 real=True reqs=0 serverconns=0`.
  - PORT before the fix: `real=False` on every case, and AlreadyConnected connected (rc=0, reqs=1). The report's `Error: serverconns=1` was already fixed by CGR-036.
  - PORT after the fix: the same three lines as REF. A library handle writes no stderr and no files here.
- ADR-0482 test: `Cgr145Tests` (Curl.Engine.UnitTests). With the production change reverted, 3 of its 4 tests failed; with it, all pass. `DeferredConnectSocketStreamTests.WriteAsync_SocketNeverConnected_ThrowsAnIOExceptionOverTheSocketException` covers the stream.
- The Ok case already matched curl and still does.
- Verified: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Networking.UnitLibrary` reports 0 failing members; `dotnet build` is clean; the fast tests pass (Curl.Engine.UnitTests 2610, Curl.Networking.UnitTests 3644). Linux 8.18 also gives rc=55 (the report). The new tests use only loopback and unconnected sockets with no platform-specific text, so they are platform-neutral.

## Log

- 2026-10-26: Created from CGR-145 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Without an open-socket callback, SOCKOPTFUNCTION gets a real socket opened before the connect; ERROR connects nothing (42) and ALREADY_CONNECTED leaves it unconnected (55), as libcurl 8.22.0
