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
completed: 2026-09-30
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The finding's "Expected" line restates the README's false claim; the defect is the README,
  not the code. ADR-0182 removed `UnsupportedFlag` and made `--http2`, `--http2-prior-knowledge`,
  `--http3` and `--http3-only` version flags, which matches real curl, so the fix makes the README
  say what the code does rather than reintroducing a refusal. Read the criterion as "the README
  no longer contradicts the reproduction's output".
- `Curl.Cli.UnitLibrary/README.md`: the `CommandLineOption` row drops `UnsupportedFlag` and adds
  the two builders it left out, `FlagThatCanRefuse` and `NextGroup`; the `CommandLineOptionTable`
  row, which also still said the four HTTP/2 and HTTP/3 options were refused (ADR-0017), now lists
  them with `-0` and `--http1.1` as non-reversible version selectors that warn on a switch.
- ADR-0137's table still mentions `UnsupportedFlag`; it is a historical decision that ADR-0182
  supersedes on this point and lies outside `touches`, so it is left as written.
- Docs-only change: build clean, fast tests green (Curl.Cli.UnitTests 3121 passed, 15 skipped).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Curl.Cli README names only builders CommandLineOption has and says --http2/--http3 select a version, as the code does
