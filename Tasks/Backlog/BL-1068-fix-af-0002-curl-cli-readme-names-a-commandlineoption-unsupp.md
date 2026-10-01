---
id: BL-1068
title: Fix AF-0002: Curl.Cli README names a CommandLineOption.UnsupportedFlag builder that does not exist and says --http2 is refused as unsupported
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-09-30
completed:
---
# BL-1068 — Fix AF-0002: Curl.Cli README names a CommandLineOption.UnsupportedFlag builder that does not exist and says --http2 is refused as unsupported

## Goal

The defect the audit office reported as AF-0002 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0002 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0002-curl-cli-readme-names-a-commandlineoption-unsuppor.md`.

Location: `Curl.Cli.UnitLibrary/README.md:14`

Location: `Curl.Cli.UnitLibrary/README.md:14`

Line 14 lists the builders 'Flag, NegatableFlag, NegatableFlagThatCanRefuse, UnsupportedFlag (no value; every spelling refused with `the installed libcurl version does not support this`, as `--http2` is)...'. CommandLineOption.cs has no UnsupportedFlag (its builders are Flag, FlagThatCanRefuse, NextGroup, NoFunctionFlag, NoFunctionValue, NegatableFlag, NegatableFlagTurnedOffByShortName, NegatableFlagThatCanRefuse, Text, FileName, Value, Subject). CommandLineOptionTable.cs:322 builds --http2 with CommandLineOption.Flag("http2", ...), which selects RequestedHttpVersion.Http2, so --http2 is not refused.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/CommandLineOption.cs,Curl.Cli.UnitLibrary/CommandLineOptionTable.cs -Pattern 'UnsupportedFlag','Flag\("http2"'
```

- Expected: UnsupportedFlag exists in CommandLineOption.cs and --http2 is refused as unsupported.
- Actual: No UnsupportedFlag match; CommandLineOptionTable.cs:322 builds --http2 with Flag(...SelectHttpVersion(RequestedHttpVersion.Http2)).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-09-30: Created.
