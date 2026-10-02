---
id: BL-647
title: Parse --tcp-fastopen and --mptcp and open those sockets on every OS that supports them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-647 — Parse --tcp-fastopen and --mptcp and open those sockets on every OS that supports them

## Goal

`--tcp-fastopen` and `--mptcp` parse on every platform and open TCP Fast Open and Multipath TCP connections on every operating system that supports them (TCP Fast Open on Windows, Linux and macOS; MPTCP on Linux, and on macOS if its sockets API allows it), whatever the platform's usual curl build does; only where the operating system itself cannot does Curl do what curl does on that OS, with curl's text.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as rarely used). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; the limit is the operating system, never what the BCL wraps (use raw socket options or raw protocol numbers where the BCL has no enum value).
- `SocketOptionName.FastOpen` exists in the BCL (Windows); Linux uses `TCP_FASTOPEN_CONNECT` or `MSG_FASTOPEN`, macOS `connectx` semantics, per curl's `lib/cf-socket.c` at tag `curl-8_21_0` (record the route per OS in Notes); Multipath TCP is `IPPROTO_MPTCP` (262) at socket creation on Linux, which the BCL's `Socket` constructor accepts as a raw protocol number, falling back to TCP as curl does when the kernel refuses it. Measure first on each OS.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: each option against a loopback URL with `-v`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [x] Tests pin parsing and the socket requested through the dialer seam per operating system; `OSCondition` separates only operating systems that lack the facility altogether.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-01 with `Record-CurlExchange.ps1`, curl 8.21.0 (Schannel, Windows), `-v` against
  `http://127.0.0.1:41647/`:
  - `--tcp-fastopen`: exit 0, the usual `Trying`/`Established connection` lines, nothing extra.
  - `--mptcp`: exit 7, stderr:
    `* failed to open socket: The system could not find the environment option that was entered.`,
    `* connect to  port 0 from  port 0 failed: No error`,
    `* Failed to connect to 127.0.0.1:41647 after 0 ms: Could not connect to server`,
    `* closing connection #0`, `curl: (7) Failed to connect to 127.0.0.1:41647 after 0 ms: Could not connect to server`.
- Measured the same day with curl 8.18.0 (OpenSSL, Linux, WSL kernel 6.6.87.2 without MPTCP), `-sv`:
  - `--mptcp http://127.0.0.1:1/`: exit 7, `* failed to open socket: Protocol not supported`,
    `* Failed to connect to 127.0.0.1 port 1 after 1 ms: Could not connect to server`.
  - `--tcp-fastopen http://127.0.0.1:1/`: exit 7, the usual `Trying` and `Connection refused` lines.
  - So curl does not fall back to TCP when the kernel refuses `IPPROTO_MPTCP`, unlike this task's
    Context assumed; Curl matches the measurement.
- Routes per OS (ADR-0317): Fast Open is `TCP_FASTOPEN` 15 on Windows (curl's Windows build sets nothing;
  Curl asks for it per the Goal), `TCP_FASTOPEN_CONNECT` 30 on Linux (curl's route), `TCP_FASTOPEN`
  `0x105` on macOS (curl uses `connectx`; filed as BL-1101). MPTCP is protocol 262 at socket creation on
  every OS; where refused, `AddressFamilyRace` writes the platform build's lines in place of `Trying`.
- Curl after the change, Windows: `--mptcp` against port 1 prints the same four lines and exit 7 as curl;
  `--tcp-fastopen` completes a transfer against a loopback listener (exit 0, body `ok`).
- Added `Curl.Console` and `Curl.Console.UnitTests` to `touches`: the options reach the dialer through
  `CurlComposition.CreateTransports`. No task in Doing on `origin/work/dark-factory` named them.
- `Measure-CodeQuality.ps1` flagged BL-646's `QualityOfServiceSocketOptions.TypeOfServiceOption` at
  complexity 14; it is now a dictionary lookup, so Curl.Networking.UnitLibrary has no failing member.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --tcp-fastopen sets TCP Fast Open on Windows, Linux and macOS and --mptcp opens IPPROTO_MPTCP sockets, failing with curl's lines where the OS refuses them
