---
id: AF-0986
title: Surviving mutant: the telnet trace names option 40 from a 40-entry table (>= -> >), throwing IndexOutOfRangeException
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:SubnegotiationOptionName-ge:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192:>=
task: BL-5788
tasks: BL-5788
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed: 2026-11-25
closed-how: mechanical
closed-by: 2026-11-25_1306.md
---
# AF-0986 - Surviving mutant: the telnet trace names option 40 from a 40-entry table (>= -> >), throwing IndexOutOfRangeException

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192`: Surviving mutant: the telnet trace names option 40 from a 40-entry table (>= -> >), throwing IndexOutOfRangeException.

## Evidence

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192`

Seeded mutation run (seed 718137, 25 mutants): 'if (option >= FirstUnnamedOption)' -> 'if (option > FirstUnnamedOption)' survived. FirstUnnamedOption is 40 and OptionNames holds options 0-39. With the mutant, a subnegotiation of option 40 skips the '40 (unknown)' branch and reads OptionNames[40], which throws IndexOutOfRangeException during the -v trace and ends the transfer. No Telnet test traces a subnegotiation of exactly option 40.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:192:>= -Member SubnegotiationOptionName -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: no | Ran the -Site command: line 192, 'option >= FirstUnnamedOption' -> '>', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-25: accepted -> closed. Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
