---
id: BL-3916
title: Fix the doubled 'Could not resolve host' -v line for an unusable --doh-url
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3914]
touches: [Curl.Engine.UnitLibrary/UnusableDohUrlResolver.cs, Curl.Networking.UnitLibrary/IDohUrlDnsResolver.cs, Curl.Engine.UnitTests/UnusableDohUrlResolverVerboseTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorUnusableDohTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3916 — Fix the doubled 'Could not resolve host' -v line for an unusable --doh-url

## Goal

Under `-v`, a run whose `--doh-url` is unusable (`bogus`, or since BL-3692 an `http` or scheme-less URL) writes `* Could not resolve host: <name>` once, as curl does, not twice.

## Context

Found by BL-3692 (CGR-163, ADR-0859). Measured 2026-10-26: `curl -sS -v --doh-url bogus http://foo.test:21802/x` on curl 8.21.0's Windows release build writes to stderr
`* Could not resolve host: foo.test`, `* Could not resolve: foo.test:21802`, `* closing connection #0`, `curl: (6) Could not resolve host: foo.test` (exit 6).
Curl writes `* Could not resolve host: foo.test` twice before `* Could not resolve: foo.test:21802`; the rest matches. Start at `Curl.Engine.UnitLibrary/UnusableDohUrlResolver.cs` (it carries the text) and whoever else reports the line (`Curl.Networking.UnitLibrary`'s `TcpConnector` / `DnsLookupFailure`); widen `touches` if the fix lands there.

## Acceptance criteria

- [x] `curl -sS -v --doh-url bogus http://foo.test:21802/x` and the same with `--doh-url http://127.0.0.1:21800/dns-query` write exactly curl's four stderr lines above, exit 6.
- [x] A focused unit test pins the single line.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- The doubled line is written by TcpConnector.ReportLookUpFailed (KindOfResolver is not DohDnsResolver), which Engine cannot influence; the fix lands in Curl.Networking.UnitLibrary/TcpConnector.cs (for example a marker so UnusableDohUrlResolver also writes the line once). BL-3914 holds TcpConnector.cs, so requeued behind it.
- 2026-10-28: Added Curl.Networking.UnitLibrary/IDohUrlDnsResolver.cs to touches: a new marker interface (no Doing task on origin/work/dark-factory names it). UnusableDohUrlResolver implements it and TcpConnector.ReportLookUpFailed writes the line once for it as for DohDnsResolver. Both measured commands now write curl's four lines, exit 6 (checked with the built curl.exe). Pinned by UnusableDohUrlResolverVerboseTests and TcpConnectorUnusableDohTests (plain and shuffled). Quality: 0 failing members across Curl.Networking.UnitLibrary and Curl.Engine.UnitLibrary. No ADR: it follows ADR-0366's DoH rule and matches the measured curl output.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: da7b5c6c d069bed7 779790c0.
- 2026-10-28: Doing -> Backlog. Needs TcpConnector.cs in Curl.Networking.UnitLibrary, held by BL-3914; depends-on BL-3914
- 2026-10-28: Backlog -> Doing. Criteria at claim: da7b5c6c d069bed7 779790c0.
- 2026-10-28: Doing -> Done. An unusable --doh-url writes 'Could not resolve host' once under -v, as curl does
