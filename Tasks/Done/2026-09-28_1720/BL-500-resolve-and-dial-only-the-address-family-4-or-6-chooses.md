---
id: BL-500
title: Resolve and dial only the address family -4 or -6 chooses
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-499]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-500 — Resolve and dial only the address family -4 or -6 chooses

## Goal

With `-4` the connector uses only IPv4 addresses and with `-6` only IPv6 addresses, for every scheme and for proxies, and a host with no address of that family fails with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 4 (Blocker). Parsing is BL-499.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `SystemDnsResolver.cs`, `ResolveOverrides.cs` (`--resolve` entries of the other family), `UdpDatagramConnector.cs` (TFTP), and the composition in `Curl.Console/CurlTransports.cs`.
- A literal address of the wrong family (`-4 http://[::1]:<P>/`) and a name resolving only to the other family are the two cases to measure.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `-4` and `-6` against `127.0.0.1`, `[::1]` and `localhost`, with `-v`; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` tests with a fake `IDnsResolver`/`ITcpDialer` show only addresses of the chosen family dialled, and the measured exit code and message (as `ConnectResult`) when none is left.
- [x] The UDP connector (TFTP) honours the family too, with a test.
- [x] A `Curl.Console.UnitTests` test shows the family reaching the connector from the command line.
- [x] New tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

- Measured curl 8.21.0 (Schannel, Windows 11) with `Record-CurlExchange.ps1 -Port 47500`, 2026-09-28. stdout was empty in every failing case and `-v` output only otherwise.
  - `-4 -v http://127.0.0.1:47500/` and `-6 -v http://127.0.0.1:47500/`: exit 0, stderr `*   Trying 127.0.0.1:47500...` / `* Established connection to 127.0.0.1 (127.0.0.1 port 47500) from 127.0.0.1 port N` / request and response lines. **A literal ignores the family.**
  - `-4 -v http://[::1]:47500/` and `-6 -v http://[::1]:47500/`: exit 7, stderr `*   Trying [::1]:47500...` / `* connect to ::1 port 47500 from :: port N failed: Connection refused` / `* Failed to connect to ::1:47500 after N ms: Could not connect to server` / `* closing connection #0` / `curl: (7) Failed to connect to ::1:47500 after N ms: Could not connect to server` (the recorder only listens on 127.0.0.1).
  - `-4 -v http://localhost:47500/`: exit 0, `* Host localhost:47500 was resolved.` / `* IPv6: ::1` / `* IPv4: 127.0.0.1` / `*   Trying 127.0.0.1:47500...` only.
  - `-6 -v http://localhost:47500/`: exit 7, the same three lines, then `*   Trying [::1]:47500...` only, `curl: (7) Failed to connect to localhost:47500 after N ms: Could not connect to server`.
  - A name with none of the family: `-6 http://github.com:47500/` exit 6 `Could not resolve host: github.com` (no resolved lines); `-6 --resolve foo:47500:127.0.0.1 http://foo:47500/` and `-4 --resolve foo:47500:[::1] ...` exit 6 `Could not resolve host: foo` after `* Negative DNS entry`; a proxy name `-6 -x http://bar:47500 --resolve bar:47500:127.0.0.1` exit 5 `Could not resolve proxy: bar`; a proxy literal `-6 -x http://127.0.0.1:47500` connects; TFTP `-6 --resolve foo:47501:127.0.0.1 tftp://foo:47501/x` exit 6, `-6 tftp://127.0.0.1:47501/x` opens 127.0.0.1.
  - `-6 --resolve foo:47500:127.0.0.1,[::1]`: `Hostname foo was found in DNS cache`, both families reported, only `[::1]` tried. `-4 http://google.com:47500/`: `IPv6: (none)`.
- Decision recorded in ADR-0143 (Decided by Claude under Stewart's delegation): the family is an `AddressFamily` constructor argument of both connectors (per option group, as the option is, so Abstractions is untouched); literals are dialled as written; `localhost` and `--resolve` entries are reported whole and dialled filtered; a looked-up answer is cached and reported with the one family.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0143 and its index row; no task in Doing names it.
- `Measure-CodeQuality.ps1` flagged `TlsVersionRange.ToSslProtocols` (complexity 12, from BL-502) in `Curl.Networking.UnitLibrary`; its loop moved into `EveryVersionBetween` so the library has no failing member. Both libraries now 100% line and branch, 0 failing members.
- Not done here, as for every exit 6: curl's later `-v` lines after a failed resolve (`Could not resolve host: foo`, `Could not resolve: foo:47500`).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -4 and -6 dial only that family's addresses for host and proxy names over TCP and TFTP; a name with none is exit 6 (5 for a proxy); literals dial as written
