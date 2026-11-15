---
id: AF-0777
title: Surviving mutant: telnet -v can print 'RCVD IAC 236' instead of 'RCVD IAC EOF'
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:CommandReceived-ge:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87:>=
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0777 - Surviving mutant: telnet -v can print 'RCVD IAC 236' instead of 'RCVD IAC EOF'

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87`: Surviving mutant: telnet -v can print 'RCVD IAC 236' instead of 'RCVD IAC EOF'.

## Evidence

Location: `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87`

events.ReportInfo("RCVD IAC " + (command >= FirstNamedCommand ? CommandNames[command - FirstNamedCommand] : command.ToString(...))) with FirstNamedCommand = 236; mutated to command > FirstNamedCommand survived. IAC 236 (EOF) then prints its number instead of curl's name, which changes the -v bytes on stderr. The trace tests cover IAC 7 and IAC NOP (241) but not the first named command.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Telnet.UnitLibrary/TelnetTraceReporter.cs:87:>= -Member CommandReceived -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
