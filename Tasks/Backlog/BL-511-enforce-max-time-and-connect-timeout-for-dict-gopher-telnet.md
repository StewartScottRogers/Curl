---
id: BL-511
title: Enforce --max-time and --connect-timeout for dict, gopher, telnet and mqtt
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-510]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-511 — Enforce --max-time and --connect-timeout for dict, gopher, telnet and mqtt

## Goal

A `dict://`, `gopher://`/`gophers://`, `telnet://` or `mqtt://`/`mqtts://` transfer still running when `-m` runs out ends with exit 28 and `Operation timed out after <ms> milliseconds with <n> bytes received`, as curl 8.21.0 does, where today it never times out.

## Context

- Conformance audit 2026-09-28, row 12 (Blocker). The mechanism is BL-498's ADR (a shared runner deadline, per-handler checks, or both); BL-510 handles the connect phase.
- Handlers: `Curl.Protocol.Dict.UnitLibrary`, `Curl.Protocol.Gopher.UnitLibrary`, `Curl.Protocol.Telnet.UnitLibrary`, `Curl.Protocol.Mqtt.UnitLibrary`; the runner is `Curl.Console/CurlCommandRunner.cs`. If the ADR puts all enforcement in the runner, the protocol projects change only where they must report bytes; leave them untouched otherwise.
- Telnet reading standard input and an MQTT subscription are the long-lived cases; measure both.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` to keep the server silent): `-m 1` for `dict://`, `gopher://`, `telnet://` and `mqtt://` against a server that sends a few bytes and stalls; stderr and exit code copied into Notes.
- [ ] One test per scheme, on a fake `TimeProvider`, pins exit 28 and the measured message with the right byte count.
- [ ] A transfer that ends before the deadline is unaffected (existing tests pass unchanged).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
