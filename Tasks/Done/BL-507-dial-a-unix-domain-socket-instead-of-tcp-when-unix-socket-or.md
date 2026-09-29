---
id: BL-507
title: Dial a Unix domain socket instead of TCP when --unix-socket or --abstract-unix-socket is given
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-507 â€” Dial a Unix domain socket instead of TCP when --unix-socket or --abstract-unix-socket is given

## Goal

With `--unix-socket <path>` the connector opens a `UnixDomainSocketEndPoint` at that path instead of resolving and dialling the URL's host (the URL's host still goes in `Host:`), `--abstract-unix-socket` uses the abstract namespace where the platform has it, and a socket that cannot be opened fails as curl 8.21.0 fails it.

## Context

- Conformance audit 2026-09-28, row 7 (Blocker). Parsing is BL-506.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `ITcpDialer.cs`/`TcpDialer.cs` (the thin adapter; ADR-0083), `ConnectDestination.cs`, `PoolingConnector.cs` and `ConnectionPoolKey.cs` (a socket path must be part of the reuse key); wiring in `Curl.Console/CurlTransports.cs`.
- `System.Net.Sockets.UnixDomainSocketEndPoint` is in the BCL and works on Windows 10+, Linux and macOS; an abstract name is a leading NUL and exists only on Linux.
- Measure: a missing path, and `-v` lines for a successful connect (curl prints `Connected to <host> (<path>)`-style lines; take the exact text from the measurement). The loopback server of `Record-CurlExchange.ps1` listens on TCP only; extend it with a `-UnixSocket <path>` listener if needed, as the root `CLAUDE.md` asks, rather than writing another server.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (extended if needed): `--unix-socket <path> -v http://localhost/` against a listening socket and a missing path, on Windows and on Linux or macOS; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` tests through the `ITcpDialer` seam show the socket endpoint dialled, no DNS lookup, and the measured failure's exit code and message.
- [x] Two transfers with different socket paths never share a pooled connection; a test shows it.
- [x] The abstract-namespace answer is pinned per platform (`OSCondition`), as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

### Scope

- `touches` widened (rule 3; no task in Doing names any of them): `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Output.UnitLibrary` (with their test projects), because the measured `Established connection` line names the socket, which the `IPEndPoint`-only `ConnectionOpenedEvent` could not carry (an additive, optional `UnixSocketRemoteIp`); `Record-CurlExchange.ps1`, extended with `-UnixSocket` as the task asked. The ADR is a new file under `Documentation/Planning/Decisions` plus its README row.
- Decisions recorded in ADR-0147 (Decided by Claude under Stewart's delegation).
- Follow-ups filed: BL-792 (HTTP's `left intact` line names the lower-cased socket path and port 0) and BL-793 (`%{remote_ip}` etc. for a socket). Sharing one pool across `--next` groups is BL-754's; ADR-0147 notes the socket must then join `ConnectionPoolKey`.

### Recorder

`Record-CurlExchange.ps1 -UnixSocket <path>` binds a Unix domain socket instead of TCP and serves the HTTP mode over it. Windows PowerShell 5.1 runs on .NET Framework, which has no `UnixDomainSocketEndPoint`, so a small C# `EndPoint` compiled with `Add-Type` binds it (C#, not a PowerShell class, so no call needs the busy main runspace).

### Measured, Windows: curl 8.21.0 (x86_64-w64-mingw32, Schannel), 2026-09-28

`-sS -v -x http://127.0.0.1:9/ --unix-socket C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock -w '\n[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}|%{num_connects}]' http://example.com:8080/x`, exit 0, stdout `hi` then `[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1|1]`, request `GET /x HTTP/1.1` / `Host: example.com:8080` / `User-Agent: curl/8.21.0` / `Accept: */*`; stderr (progress lines dropped):

```
*   Trying C:\Users\Stewart Rogers\AppData\Local\Temp\bl:0...
* Established connection to C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock (C:\Users\Stewart Rogers\AppData\Local\Temp\bl port 0) from  port 0 
* using HTTP/1.x
> GET /x HTTP/1.1
> Host: example.com:8080
...
* Connection #0 to host c:\users\stewart rogers\appdata\local\temp\bl507.sock:0 left intact
```

`-sS -v --unix-socket C:\Users\Public\s.sock http://localhost/a http://LocalHost/b` (short path): `*   Trying C:\Users\Public\s.sock:0...`, `* Established connection to C:\Users\Public\s.sock (C:\Users\Public\s.sock port 0) from  port 0 `, `* Connection #0 to host c:\users\public\s.sock:0 left intact`, then `* Reusing existing http: connection with host LocalHost` for the second URL.

Failures (`curl -v <args> http://localhost/`), each exit 7, no request:

| Arguments | stderr |
| --- | --- |
| `--unix-socket nosuch.sock` | `*   Trying nosuch.sock:0...` / `* Immediate connect fail for nosuch.sock: Connection refused` / `* connect to nosuch.sock port 0 from  port 0 failed: Connection refused` / `* Failed to connect to localhost:80 over unix://nosuch.sock after 0 ms: Could not connect to server` / `* closing connection #0` / `curl: (7) Failed to connect to localhost:80 over unix://nosuch.sock after 0 ms: Could not connect to server` |
| `--unix-socket C:/nope/x.sock` (no such directory) | the same lines with `Network down` |
| `--abstract-unix-socket x` | `*   Trying :0...` / `* Immediate connect fail for : Invalid arguments` / `* connect to  port 0 from  port 0 failed: Invalid arguments` / `... over unix://x after 0 ms ...` |
| a refused one with `-w` | `[|-1||-1|0]` |

- `--unix-socket <107 a's>`: exit 7 (dialled); `<108 a's>` and `<120 a's>`: exit 6 `curl: (6) Unix socket path too long: '<path>'`; `--abstract-unix-socket <107 a's>`: exit 7.
- `--unix-socket C:\Users\Public\nosuch.sock -x "http://[bad" http://localhost/`: exit 7 over the socket; without `--unix-socket`, exit 5 `Unsupported proxy syntax in 'http://[bad': Bad IPv6 address`. The proxy is dropped unread.
- .NET on Windows raises `SocketError.ConnectionRefused`, `NetworkDown` and `InvalidArgument` for the three failures above (probed with a file-based app), matching curl's words.

### Measured, Linux: curl 8.18.0 (Ubuntu, OpenSSL 3.5.5) in WSL, 2026-09-28

No 8.21.0 build exists there; a listener was `nc -lU`. Each `curl -sS -v <args>`:

| Arguments | Exit | stderr |
| --- | ---: | --- |
| `--unix-socket nosuch.sock http://localhost/` | 7 | `*   Trying nosuch.sock:0...` / `* Immediate connect fail for nosuch.sock: No such file or directory` / `* Failed to connect to localhost over nosuch.sock after 0 ms: Could not connect to server` / `* closing connection #0` / `curl: (7) Failed to connect to localhost over nosuch.sock after 0 ms: Could not connect to server` |
| `--unix-socket /nope/x.sock http://localhost/` | 7 | the same with `/nope/x.sock`, `No such file or directory` |
| `--abstract-unix-socket nosuchabs http://localhost/` | 7 | `*   Trying :0...` / `* Immediate connect fail for : Connection refused` / `... over nosuchabs ...` |
| `--unix-socket /tmp/notasocket http://localhost/` | 7 | `Connection refused` |
| `--unix-socket /tmp/l.sock http://example.com:8080/x` (listening) | 0 | `*   Trying /tmp/l.sock:0...` / `* Established connection to example.com (/tmp/l.sock port 0) from  port 0 ` / ... / `* Connection #0 to host example.com:8080 left intact`; request `GET /x HTTP/1.1`, `Host: example.com:8080`; stdout `hi` |
| `--abstract-unix-socket abs1 http://localhost/` (listening on `@abs1`) | 0 | `*   Trying :0...` / `* Established connection to localhost ( port 0) from  port 0 ` |
| `-x http://127.0.0.1:9/ --unix-socket /tmp/l.sock http://example.com/` | 0 | origin-form `GET / HTTP/1.1` to the socket: the proxy is ignored |

8.18.0's wording differs from 8.21.0's (no `unix://`, no `:<port>`, no `connect to` line, the URL's host in `Established`); the text is curl's common code, so ADR-0147 takes 8.21.0's everywhere and only the OS reason differs.

### Ours against curl

`dotnet run --project Curl.Console --no-build` through the recorder, same arguments: the `-v` lines, request bytes, stdout and exit codes match curl 8.21.0 for the listening socket (with a bad `-x`) and the missing one, except the `left intact` line (BL-792) and the elapsed milliseconds.

### Tests

- `Curl.Networking.UnitTests`: `TcpConnectorTests.UnixSocket` (12), `UnixSocketAddressTests` (13 cases), `ConnectFailureReasonTests` (+2 rows), `TcpDialerTests` (+1 argument check, +2 Integration loopback tests). The abstract answer is pinned per platform: Windows `Invalid arguments` (`OSCondition(Windows)`), Linux connects with an empty name (`OSCondition(Linux)`).
- `Curl.Output.UnitTests`: `VerboseTransferEventWriterTests.ConnectionOpenedThroughAUnixSocket_NamesTheSocketAsCurl` (3 rows).
- `Curl.Console.UnitTests`: `CurlCompositionUnixSocketTests` (6), including `OptionGroupsWithDifferentSockets_NeverShareAPooledConnection` and the proxy dropped unread.
- Fast tests: all green (Networking 940, Console 1339, Output 414, Abstractions 578). Measure-CodeQuality: Curl.Networking.UnitLibrary, Curl.Console, Curl.Output.UnitLibrary and Curl.Protocol.Abstractions.UnitLibrary each 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --unix-socket and --abstract-unix-socket dial the socket in place of the host, port and proxy, with curl 8.21.0's -v lines and exit 7/exit 6 failures
