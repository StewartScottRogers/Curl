---
id: BL-3172
title: Fail a port 0 URL with exit 7 and guard Curl.Console against unhandled exceptions
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/UnhandledExceptionGuard.cs, Curl.Console.UnitTests/UnhandledExceptionGuardTests.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorPortZeroTests.cs, Documentation/Planning/Decisions/README.md, Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Protocol.Abstractions.UnitLibrary/ProxyEndpoint.cs, Curl.Protocol.Abstractions.UnitTests/ConnectTargetTests.cs, Curl.Protocol.Abstractions.UnitTests/ProxyEndpointTests.cs, Curl.Core.UnitLibrary/ProxyUrlParser.cs, Curl.Core.UnitTests/ProxyUrlParserTests.cs, Curl.Core.UnitTests/CoreParserAdversarialTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3172 — Fail a port 0 URL with exit 7 and guard Curl.Console against unhandled exceptions

## Goal

A URL with port 0 fails with curl's exit 7 and message instead of crashing the process, and no unhandled exception can ever kill `curl.exe` without a `curl: (N)` message and a normal exit code.

## Context

Stewart reported on 2026-10-22: `curl -s http://127.0.0.1:0/` (also `http://a:0/` and `http://[::1]:0/`). Measured with the preview build (0.2.0-rolling.180): Curl dies with `Unhandled exception. System.ArgumentOutOfRangeException: Port ('0') must be greater than or equal to '1'` and exit code 0xC0000409; Windows System32 curl 8.21.0 Schannel exits 7 with `curl: (7) Failed to connect to 127.0.0.1:0 after 0 ms: Could not connect to server`. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the exact text for IPv4, hostname and IPv6 literal, for https, ftp and through an HTTP proxy and a SOCKS proxy (port 0 on the proxy URL too), and for Linux/macOS if it differs (pin each in its own `[OSCondition]` test).

Part 1, port 0: find where the port reaches a BCL call that rejects 0 (`Curl.Networking.UnitLibrary/TcpDialer.cs` and the connect loop in `TcpConnector.cs` near line 2398; a hostname connect with port 0 throws `ArgumentOutOfRangeException` from `Socket.ConnectAsync(host, port)`), and make port 0 produce the connect failure curl gives.

Part 2, the top-level guard: `Curl.Console/Program.cs` `Main` has no handler. Add one that turns any other unhandled exception (other than cancellation) into a `curl: (N) <message>` line on standard error and a normal exit code. Choose N by measuring what upstream curl does for an internal failure of that kind (the nearest codes are 2 `CURLE_FAILED_INIT`, 4 `CURLE_NOT_BUILT_IN` and 8 `CURLE_WEIRD_SERVER_REPLY`; 2 is the likely pick) and record the choice and why in a new ADR marked "Decided by Claude under Stewart's delegation" (next free ADR number in `Documentation/Planning/Decisions/`, update its README index). The guard must not swallow the exit code of a normal run, must flush standard output first, and must stay native-AOT compatible (no reflection). `Program.Main` is hard to unit test, so put the guard in a small testable class in `Curl.Console` (for example `UnhandledExceptionGuard.cs`) that `Main` calls.

## Acceptance criteria

- [x] Tests in `Curl.Networking.UnitTests/TcpConnectorPortZeroTests.cs` show port 0 over http (IPv4 literal, hostname, `[::1]`), https, ftp and through an HTTP proxy and a SOCKS proxy each return exit 7 with the text curl 8.22.0 measured (`Failed to connect to <host> port 0 after N ms: Could not connect to server` or the measured form), with no exception escaping.
- [x] A test in `Curl.Console.UnitTests/UnhandledExceptionGuardTests.cs` forces an exception inside a transfer (a fake handler that throws `InvalidOperationException`) and expects a single `curl: (N) ...` line on standard error and the chosen exit code, not a thrown exception; another expects an `OperationCanceledException` to keep its existing handling.
- [x] The ADR for the choice of N exists, is listed in the ADR index, and names the measurement it rests on.
- [x] `dotnet build Curl.Networking.UnitLibrary Curl.Console -warnaserror` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pass; no test touches the network.

## Notes

- Measured curl 8.22.0 (Git for Windows ucrt64) with `Record-CurlExchange.ps1 -NoServer` on 2026-10-22: every port 0 case is exit 7, `Failed to connect to <host>:0 after 0 ms: Could not connect to server` (`::1:0` for `[::1]`), `127.0.0.1:0 over proxy 127.0.0.1` for an HTTP forward proxy on port 0, and `example.test:80 over proxy 127.0.0.1` (`:443` for https) through a SOCKS5 or tunnelling proxy on port 0. Table in ADR-0766. Not measured on Linux/macOS: Curl now fails port 0 before any socket, so its answer is the same text on every platform, and the tests use fakes only (platform-neutral, no network).
- The throw was not in `TcpDialer` but in `ConnectTarget`/`ProxyEndpoint`'s port guard (Curl.Protocol.Abstractions), and `ProxyUrlParser` (Curl.Core) failed a port 0 proxy early with the HTTP-proxy wording, wrong for SOCKS. Added those files and their tests to `touches`; no task in Doing on origin/work/dark-factory names them (BL-3173 holds other Abstractions and Core files). `TcpDialer.cs` needed no change.
- Default taken: a port 0 dial reports `SocketError.ConnectionRefused` as its last error (`%{errno}` not measured).
- The guard test drives `UnhandledExceptionGuard.RunAsync` with a run delegate that throws `InvalidOperationException`, which is what a throwing handler escaping the runner amounts to; `OperationCanceledException` escapes unchanged as before. Exit 2 chosen (ADR-0766).
- Measure-CodeQuality.ps1 for Networking, Abstractions, Core and Console: 0 failing members. Fast tests of Networking, Console, Abstractions and Core UnitTests green. CI on Linux/macOS: changes are platform-neutral, so ticked.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a5477d9d d4af9781 3ec186c5 9cedf5fd.
- 2026-10-22: Doing -> Done. Port 0 fails with exit 7 as curl 8.22.0 does; unhandled exceptions end with curl: (2) and exit 2 (ADR-0766)
