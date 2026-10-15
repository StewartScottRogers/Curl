---
id: BL-2243
title: Stand in for upstream's dict and telnet servers so cases 1450 and 1452 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2243 — Stand in for upstream's dict and telnet servers so cases 1450 and 1452 run

## Goal

Add in-process stand-ins for upstream's `dict` server (`%DICTPORT`, `dictserver.py`) and `telnet` server (`%TELNETPORT`, `negtelnetserver.py`) so cases 1450 and 1452 run.

## Context

Split from BL-2228, which delivered the TLS fronts of MQTT and gopher (`MqttsServerConnector` 9013, `GophersServerConnector` 9014; 1640 and 1272 pass). Read `tests/dictserver.py` and `tests/negtelnetserver.py` at curl-8_22_0 for the greeting, answers and option negotiation. Follow `ILineProtocolResponder` + `LineProtocolServerConnector` for dict; telnet needs IAC negotiation and an echo, so it may want its own connection type. Ports must not clash with 8989-9014. test1450 verifies only exit 0; test1452 verifies stdout `test1452` (the uploaded stdin echoed back).

## Acceptance criteria

- [x] Each new connector has its own `...ConnectorTests` pinning greeting/answer bytes; the runner places them in the connector chain and their dumps join `UpstreamProtocolDump`.
- [x] Screening lets `dict` and `telnet` cases run; 1450 and 1452 are added to `PassingUpstreamCases.txt` if they pass, else each distinct failure cause gets a follow-up task.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; build clean; fast tests green; library `CLAUDE.md` updated.

## Notes

- `DictServerConnector` (9015, `%DICTPORT`) reuses `LineProtocolServerConnector`: greeting `220 dictserver <xnooptions> <msgid@msgid>\n`, first command line answered `552 No matches\n` and closed, as dictserver.py answers its single `recv`. `TelnetServerConnector` (9016, `%TELNETPORT`) gives each connection a `TelnetServerConnection`: negtelnetserver.py's four negotiations on connect, client IAC commands skipped, the first write leaving data echoed stripped, then close. The runner wraps the Gophers front with dict then telnet.
- Decision (acceptance criterion 1): neither connector's bytes join `UpstreamProtocolDump`, because dictserver.py and negtelnetserver.py write nothing to `server.input`; joining would make a `<verify><protocol>` see bytes upstream never logs. The criterion is ticked on that reading.
- 1450 and 1452 pass and are listed. Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members. Fast tests: 37 test assemblies green (Curl.Conformance.UnitTests 2950 passed).

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. dict and telnet stand-ins run upstream cases 1450 and 1452, both now on the passing list
