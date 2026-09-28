---
id: BL-482
title: Print curl's 'Host H:P was resolved.', IPv6 and IPv4 lines and --resolve's 'Added ... to DNS cache' line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-482 — Print curl's 'Host H:P was resolved.', IPv6 and IPv4 lines and --resolve's 'Added ... to DNS cache' line

## Goal

With `-v`, a connect to a host name prints curl 8.21.0's `Host H:P was resolved.`, `IPv6: ...` and `IPv4: ...` lines before `Trying`, and each `--resolve` entry prints `Added H:P:A to DNS cache`, as curl does.

## Context

- Found in BL-481 (ADR-0113). Measured 2026-09-27 with curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1`, `-s -v`:
  - `http://localhost:P/a` prints `Host localhost:P was resolved.`, `IPv6: ::1`, `IPv4: 127.0.0.1`, then `Trying [::1]:P...`; the second URL prints `Hostname localhost was found in DNS cache` then the same three lines again.
  - `--resolve foo.example:P:127.0.0.1` prints `Added foo.example:P:127.0.0.1 to DNS cache` and `Hostname foo.example was found in DNS cache`, then `Host foo.example:P was resolved.`, `IPv6: (none)`, `IPv4: 127.0.0.1`, for each transfer.
  - An IP literal (`http://127.0.0.1:P/`) prints none of the three lines.
- `TcpConnector.ResolveAsync` in `Curl.Networking.UnitLibrary` is where the cache line is reported today; where `Added` belongs (per transfer, as measured, or once) needs measuring with one URL and two.

## Acceptance criteria

- [x] A `TcpConnector` test pins the three resolved lines for a host name, first and second connect, and none for an IP literal.
- [x] A test pins `Added H:P:A to DNS cache` for a `--resolve` entry in curl's position.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-481 (2026-09-27).
- Measured 2026-09-27 (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1`): `Added` is printed at the start of every transfer (URL), not for a followed `-L` redirect; from the second transfer each is preceded by `RESOLVE H:P - old addresses discarded`; `+` entries add ` (non-permanent)`, `*` entries add `RESOLVE *:P using wildcard`; removals print nothing; the address list is echoed verbatim; `Host H:P was resolved.` names the host as cached (the entry's text, `*`, or the name looked up); a bad entry comes after the `Added` lines of those before it. Full table in ADR-0114.
- Decision (ADR-0114): `ResolveOverrides.Entries` (new `ResolveEntry`) feeds `TcpConnector.LoadResolveEntries(events)`, meant for the console at each transfer's start; until it is called, `ConnectAsync` loads the entries once itself, so the first transfer already matches curl. Tried telling transfers apart by their `ITransferEvents` first, but the console shares one events object across the run.
- Console wiring filed as BL-486 (`Curl.Console` is held by BL-458 in another lane).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0114 and its index row; no task in Doing names it.
- Known gap: the `IPAddress.TryParse` test for "name is an IP address" also accepts shorthand like `1`; URL parsing normalises those before they get here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v prints curl's Host H:P was resolved., IPv6: and IPv4: lines after every lookup, and --resolve's Added H:P:A to DNS cache lines on the first transfer (every transfer once BL-486 wires the console)
