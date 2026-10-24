---
id: BL-3426
title: Write the differential case files for the tester's cases_proxy.py cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233, BL-3235, BL-3284, BL-3285, BL-3286, BL-3287, BL-3288, BL-3334]
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proxy, Curl.Conformance.Differential.UnitTests/Protocols/Proxy]
requirement: none
created: 2026-10-23
completed:
---
# BL-3426 — Write the differential case files for the tester's cases_proxy.py cases

## Goal

The tester's `cases_proxy.py` cases (35) exist as differential case files under `Cases/CliProtocols/Proxy`.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\cases_proxy.py` (Python is a reference only; never commit or run it). Three cases use `{PKI}` (BL-3235); `proxy-tunnel-*` cases use `{U}` (BL-3233's loopback server); eleven `px-socks*` and `px-preproxy` cases need BL-3334's SOCKS port. The runner sets no proxy environment per case, so `DifferentialCase` needs no environment field. Generate the case JSON with a PowerShell script, not by hand. Parent: BL-3234, BL-3217.

## Acceptance criteria

- [ ] One case file per `cases_proxy.py` case exists under `Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proxy`, with the same name, arguments and expected server behaviour as the tester's case.
- [ ] A unit test in `Curl.Conformance.Differential.UnitTests/Protocols/Proxy` loads every case file and checks the count and that each parses; it is platform-neutral.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows.
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split from BL-3234 (over its cost cap) on 2026-10-23.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: d9c78f7b 41c603dd 1f9665bb 5e81fa96.
