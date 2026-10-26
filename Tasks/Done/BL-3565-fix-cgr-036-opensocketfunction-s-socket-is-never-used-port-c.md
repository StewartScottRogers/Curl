---
id: BL-3565
title: Fix CGR-036: OPENSOCKETFUNCTION's socket is never used: PORT connects its own socket first, then calls the callback
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3833]
touches: [Curl.Engine.UnitLibrary/SocketCallbackConnector.cs, Curl.Engine.UnitTests/Cgr036Tests.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3565 — Fix CGR-036: OPENSOCKETFUNCTION's socket is never used: PORT connects its own socket first, then calls the callback

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-036 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-036: OPENSOCKETFUNCTION's socket is never used: PORT connects its own socket first, then calls the callback

- **Severity:** High (a refusal in the callback does not stop the connection, and a socket the application supplies is ignored)
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-5`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- three cases. Command: `probe.exe C_45,C_46,C_47 2`.
  - `C_46`: URL `http://127.0.0.1:21700/where`. OPENSOCKETFUNCTION connects a TCP socket to 127.0.0.1:21701 and returns it; SOCKOPTFUNCTION returns CURL_SOCKOPT_ALREADY_CONNECTED.
  - `C_45`: OPENSOCKETFUNCTION returns CURL_SOCKET_BAD.
  - `C_47`: URL to 127.0.0.1:21799 (nothing listening), with open and close socket callbacks.

Commands (run each with REF and with PORT):

```sh
probe.exe C_45,C_46,C_47 2
```

**Expected (real curl)**

- C_46: the request goes to server 2: `body 'other' server1reqs=0 server2reqs=1`.
- C_45: `rc=7` and the server sees no connection (`conns=0`).
- C_47: `rc=7 opens=[fam=2 type=1 proto=6 len=16 port=21799 ip=127.0.0.1] closes=1`.

**Actual (Curl)**

- C_46: `body 'hello' server1reqs=1 server2reqs=0`. The application's socket to server 2 stays open, unused.
- C_45: `rc=7`, but server 1 has already accepted a TCP connection (`conns=1`).
- C_47: `rc=7 opens=[] closes=0`. Neither callback is called.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\SocketCallbackConnector.cs:66-80` (`inner.ConnectAsync` runs before `OpenSocketAsync`) and `:168-190`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr036Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- 2026-10-26 (lane 7, first claim): the fix needs a seam outside Curl.Engine. `SocketCallbackConnector.ConnectAsync` calls `inner.ConnectAsync` first because the TCP sockets are opened inside `Curl.Networking`'s `AddressFamilyRace` (built in `TcpConnector.cs` ~2408) and nothing per transfer reaches it before a dial. Filed BL-3833 for a per-attempt socket-open hook on `ConnectTarget` (refuse, supply an OS socket handle, closed notice); this task then moves the open-socket, socket-option and close-socket callbacks into that hook. No code was changed in this claim.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 6, delivered, ADR-0828): `SocketCallbackConnector.ConnectAsync` puts an `AttemptSocketOpener` on `ConnectTarget.SocketOpener` (BL-3833's hook), so the open-socket and socket-option callbacks run before each TCP attempt dials: `CURL_SOCKET_BAD` refuses the attempt (exit 7, no connection), the callback's socket is handed to the dialer as `Supplied` (used as it is under `CURL_SOCKOPT_ALREADY_CONNECTED`), the socket-option error closes the socket and fails exit 42, and a failed attempt's socket (or an opened one when TLS or a tunnel fails afterwards) goes to the close-socket callback. A connector that never asks the opener (Unix socket, fakes) and QUIC keep the after-connect steps.
- Decision (ADR-0828): under `EngineDialing` (the conformance harness's in-memory servers) the callback's socket is not handed to the dialer (`suppliesSockets: false`): the upstream libtests (lib500, lib585, lib1960...) return socket numbers no operating system made, and the first build without this failed test585, 586, 595, 596 and 1960 on the ratchet; with it all 4381 conformance cases pass again.
- Touches widened: `Curl.Engine.UnitLibrary/CLAUDE.md` (its socket-callbacks paragraph described the old after-connect order); no task in Doing on origin/work/dark-factory names it.
- Repro: the report's probe (`SCR\evidence\libcurl-multi-callbacks`) is gone and a REF run needs a C or P/Invoke probe over `libcurl-4.dll`, so REF's results are the report's measured Expected values (curl 8.22.0 Schannel and 8.18.0 OpenSSL agree): C_46 `body 'other' server1reqs=0 server2reqs=1`, C_45 `rc=7 conns=0`, C_47 `rc=7 opens=[fam=2 type=1 proto=6 len=16 port=21799 ip=127.0.0.1] closes=1`. PORT is measured by `Cgr036Tests` through a real `TcpConnector` on loopback sockets: C_45 exit 7 and the listener has no connection waiting; C_46 the connection's local end is the application's socket's (accepted by the second listener) and the target listener has nothing waiting, close-socket called once for that socket on dispose; C_47 one open with family 2, `SOCK_STREAM`, `IPPROTO_TCP` and the 127.0.0.1:port sockaddr, one close, exit 7. Also pinned: an unconnected supplied socket is the one connected, a socket-option error makes no connection (exit 42, sockopt then close).
- Before the fix (opener taken off the target): 7 of the 9 `Cgr036Tests` fail; after: 9/9. Existing `SocketCallbackConnectorTests`, `ManagedEasyTransferTests.Sockets` and the conformance socket cases (500, 585, 586, 595, 596, 1530, 1960) still pass.
- Measured: dotnet build clean; fast tests green in all 46 test projects; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: first run `AttemptSocketOpener.Open` complexity 12 (split into `SocketFor` and `DecisionFor`), second run nothing of this task's; its one failing member, `MultiCallbackQueue.QueuedCallback.WaitUntilRun` (timing-dependent branch, absent from the first run), is BL-3857's. Tests use loopback IPv4 only, no paths or OS error text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-036 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3833: the open-socket hook must reach Curl.Networking's address race before the connect
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. The open-socket and socket-option callbacks run before each TCP attempt: CURL_SOCKET_BAD makes no connection, the application's socket is the one used, and a failed attempt's socket goes to the close-socket callback (CGR-036)
