---
id: BL-3221
title: Replay the tester's CLI non-HTTP protocol and proxy cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3220]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols, Curl.Conformance.Differential.UnitTests/Protocols, Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols, Curl.Conformance.Differential.IntegrationTests/CliProtocols]
requirement: none
created: 2026-10-22
completed:
---
# BL-3221 — Replay the tester's CLI non-HTTP protocol and proxy cases in the differential runner

## Goal

The differential runner replays the tester's CLI non-HTTP protocol and proxy cases (`cases_proto.py`, `cases_proto2.py`, `cases_proxy.py`, about 130 cases) on C# ports of `proto_servers.py`, `proto2_servers.py` and `proxy_server.py`.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3218. Reports to compare with: `report_proto.txt`, `report_proto2.txt`, `report_proxy.txt`. Split further with task-planner if the servers are more than one run.

## Acceptance criteria

- [ ] Case files and server ports for the proto, proto2 and proxy cases exist, and one run's report is recorded under Notes with each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
