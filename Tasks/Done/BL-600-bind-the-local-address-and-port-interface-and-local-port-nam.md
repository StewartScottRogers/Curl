---
id: BL-600
title: Bind the local address and port --interface and --local-port name, failing with exit 45
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-599]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-600 — Bind the local address and port --interface and --local-port name, failing with exit 45

## Goal

The TCP connector binds the socket to the address `--interface` names (an interface name, a host name or an address, per its prefix) and to the first free port in the `--local-port` range before connecting, and a name or port that cannot be bound fails with exit 45 (`CURLE_INTERFACE_FAILED`) and curl 8.21.0's message.

## Context

- Conformance audit 2026-09-28, row 13 (Major). Parsing is BL-599.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `ITcpDialer.cs`/`TcpDialer.cs` (ADR-0083: the adapter is thin, so unit tests go through `ITcpDialer`), `SystemNetworkInterfaceLookup.cs` and `INetworkInterfaceLookup` (already used for FTP active mode, ADR-0110), and the connection pool key (a bound address must not share a connection with an unbound one).
- Interface names differ by platform; measure on Windows and on Linux or macOS.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--interface 127.0.0.1`, `--interface bogus0`, `--interface if!lo` (Linux) or the loopback adapter name (Windows), `--local-port 40000-40010 -w '%{local_port}'`, and a port range already all in use; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the bind requested through the dialer seam and exit 45 with the measured message for each failure; each platform's interface-name answer is pinned under `OSCondition`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

- From BL-599 (parsing): `CommandLineOptions.Interface` is an `InterfaceBinding` (`InterfaceOrHostName`,
  `InterfaceName`, `HostName`, `IsMalformed`) and `CommandLineOptions.LocalPorts` a `LocalPortRange`
  (`First`, `Last`, `Count`). A value with `IsMalformed` set must fail the transfer before connecting with
  exit 43 and `curl: (43) setopt 0x274e got bad argument` (curl 8.21.0, Windows, measured). An `ifhost!`
  interface part longer than 254 characters fails at connect instead, with exit 43 and
  `curl: (43) Failed to connect to <host>:<port> after 0 ms: A libcurl function was given a bad argument`.
  Unbindable names measured on Windows: `nosuchif`, `if!nosuch`, ` `, `IF!`, `ifhost!!h` all give exit 45
  `curl: (45) Failed to connect to 127.0.0.1:47599 after <n> ms: Failed binding local connection end`.

### Measured 2026-09-29 (curl 8.21.0 Schannel, Windows; `Record-CurlExchange.ps1 -Port 47599 ... -s -S`)

| Arguments | stdout | stderr | exit |
| --- | --- | --- | --- |
| `--interface 127.0.0.1 -w %{local_ip}` | `127.0.0.1` | | 0 |
| `--interface bogus0` | | `curl: (45) Failed to connect to 127.0.0.1:47599 after 2732 ms: Failed binding local connection end` | 45 |
| `--interface "Loopback Pseudo-Interface 1"` (the loopback adapter's name) | | same exit 45 line, `after 2 ms` | 45 |
| `--interface "if!Loopback Pseudo-Interface 1"` | | same exit 45 line, `after 0 ms` | 45 |
| `--local-port 40000-40010 -w %{local_port}` | `40000` | | 0 |
| `--local-port 40000-40002`, all three in use | | same exit 45 line, `after 0 ms` | 45 |
| `--local-port 40000-40005 -w %{local_port}`, 40000-40002 in use | `40003` | | 0 |
| `--local-port 40001`, in use | | same exit 45 line | 45 |
| `--interface host!localhost` (with or without `-4`) | | `curl: (7) Failed to connect to 127.0.0.1:47599 after 0 ms: Could not connect to server` | 7 |
| `--interface ::1` | | same exit 7 line | 7 |
| `--interface host!nosuch.invalid`, `--interface 192.0.2.1` | | exit 45 line | 45 |
| `--interface ifhost!Ethernet!127.0.0.1 -w %{local_ip}` | `127.0.0.1` | | 0 |
| `--interface ifhost!<255 a's>!127.0.0.1 -w [%{exitcode}]` | `[43]` | `curl: (43) Failed to connect to 127.0.0.1:47599 after 0 ms: A libcurl function was given a bad argument` | 43 |
| `--interface bogus0 -x http://127.0.0.1:47599 http://example.com/` | | `curl: (45) Failed to connect to 127.0.0.1:47599 over proxy 127.0.0.1 after 2756 ms: Failed binding local connection end` | 45 |
| `-w [%{exitcode}]\n --interface if! http://…/a http://…/b` | `[43]` | `curl: (43) setopt 0x274e got bad argument`; nothing requested, `/b` not tried | 43 |
| `-v --interface if!` | | `* setopt 0x274e got bad argument`, then the error line | 43 |
| `http://…/a --next --interface if! http://…/b` | | `/a` requested, then the exit 43 line | 43 |

`-v` for `--interface host!nosuch.invalid http://localhost:47599/`: each address gets `Trying`, `Could not resolve host: nosuch.invalid`,
`Could not bind to 'nosuch.invalid' with errno 0: No error`, `connect to ::1 port 47599 from  port 0 failed: No error`, and then
`Failed to connect to localhost:47599 after 91 ms: Failed binding local connection end`: a bind failure moves on to the next address,
and the last address's failure decides the exit. `--interface 127.0.0.1` to `localhost` fails `::1` (family) and connects `127.0.0.1`.
Other `-v` lines measured, not yet written (BL-1027): `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: N`,
`Bind to local port 40000 failed, trying next`, `bind failed with errno 10048: Address already in use`, `Could not bind to interface 'Ethernet' with errno 0: No error`.

Linux (curl 8.18.0 OpenSSL under WSL Ubuntu, older than the reference, measured for the platform answer only): `--interface lo` and
`if!lo` bind (the connect went on; my listener had not started, so exit 7), `if!bogus0` is exit 45
(`Could not bind to interface 'bogus0' with errno 19: No such device`), `bogus0` exit 45. So interface names are looked up off
Windows and never found on Windows, as `SystemNetworkInterfaceLookup` already answers (ADR-0110).

### Decisions (ADR-0269, decided by Claude under Stewart's delegation)

- Seam: `ITcpDialer.DialFromAsync(endPoint, localEndPoint, localPortCount)`; `TcpDialer.BindLocalEnd` walks the port range
  (unit tested over real unconnected sockets, like `TcpConnectionListenerTests`); `TcpConnector` takes `LocalBinding` and
  wraps its dialer in `LocalBindingTcpDialer`, which chooses the local address per dialled family. Unix sockets are not bound.
- A failed bind is a `LocalBindException` (a `SocketException`), so the race moves on; the last `LocalBindFailure` gives exit 45,
  43 or 7. The console refuses an `IsMalformed` value before connecting (`InterfaceSetoptFailure`, run-ending).
- `LocalBinding` mapping in `CurlComposition.LocalBindingOf`: plain name = interface then host; `if!` interface only; `host!`
  host only; `ifhost!` host plus device name (length check only; `SO_BINDTODEVICE` is BL-1026).
- `localhost` in a bind resolves to `::1` first whatever `-4` says (measured exit 7 with `-4`).
- Off Windows the `from  port 0 failed:` reason is glibc's `Success` for errno 0 - not measured on 8.21.0 (BL-1027 measures it).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0269 and its README row; no task in Doing names it (BL-888 touches SSH only).
- `--ai-help`: no option was added or renamed (parsing is BL-599), so its text is unchanged.

Follow-ups filed: BL-1027 (`-v` bind lines), BL-1024 (forward-proxy `over proxy` in exit 7/45 messages, pre-existing),
BL-1025 (QUIC's UDP socket), BL-1026 (`SO_BINDTODEVICE` on Linux).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --interface and --local-port bind every TCP dial; unbindable names and busy ranges fail with exit 45, an over-long ifhost! device with exit 43, a setopt-refused value with exit 43 before connecting
