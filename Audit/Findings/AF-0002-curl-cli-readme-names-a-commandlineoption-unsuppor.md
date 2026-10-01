---
id: AF-0002
title: Curl.Cli README names a CommandLineOption.UnsupportedFlag builder that does not exist and says --http2 is refused as unsupported
auditor: truthfulness
severity: Medium
status: accepted
key: truthfulness:Curl.Cli.UnitLibrary/README.md:CommandLineOption.UnsupportedFlag:false-statement
task: BL-1068
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed:
closed-by:
---
# AF-0002 - Curl.Cli README names a CommandLineOption.UnsupportedFlag builder that does not exist and says --http2 is refused as unsupported

## Summary

Medium finding from the truthfulness auditor at `Curl.Cli.UnitLibrary/README.md:14`: Curl.Cli README names a CommandLineOption.UnsupportedFlag builder that does not exist and says --http2 is refused as unsupported.

## Evidence

Location: `Curl.Cli.UnitLibrary/README.md:14`

Line 14 lists the builders 'Flag, NegatableFlag, NegatableFlagThatCanRefuse, UnsupportedFlag (no value; every spelling refused with `the installed libcurl version does not support this`, as `--http2` is)...'. CommandLineOption.cs has no UnsupportedFlag (its builders are Flag, FlagThatCanRefuse, NextGroup, NoFunctionFlag, NoFunctionValue, NegatableFlag, NegatableFlagTurnedOffByShortName, NegatableFlagThatCanRefuse, Text, FileName, Value, Subject). CommandLineOptionTable.cs:322 builds --http2 with CommandLineOption.Flag("http2", ...), which selects RequestedHttpVersion.Http2, so --http2 is not refused.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/CommandLineOption.cs,Curl.Cli.UnitLibrary/CommandLineOptionTable.cs -Pattern 'UnsupportedFlag','Flag\("http2"'
```

- Expected: UnsupportedFlag exists in CommandLineOption.cs and --http2 is refused as unsupported.
- Actual: No UnsupportedFlag match; CommandLineOptionTable.cs:322 builds --http2 with Flag(...SelectHttpVersion(RequestedHttpVersion.Http2)).

## Re-audits

