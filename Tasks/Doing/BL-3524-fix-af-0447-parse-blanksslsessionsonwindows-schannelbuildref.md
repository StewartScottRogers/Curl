---
id: BL-3524
title: Fix AF-0447: Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3524 — Fix AF-0447: Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names

## Goal

The defect the audit office reported as AF-0447 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0447 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0447-parse-blanksslsessionsonwindows-schannelbuildrefus.md`.

Location: `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:114`

Location: `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:114`

The test calls Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason(SslSessions). That helper (lines 103-110) asserts only CollectionAssert.DoesNotContain(result.Refusal?.StandardErrorLines ?? [], blankLine). It passes when the parse is accepted (Refusal null) as well as when it is refused for any reason. The name promises the Schannel build refuses --ssl-sessions as unsupported, but no exit code or message is asserted.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs -Pattern 'void Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason' -Context 0,7
```

- Expected: The Windows test asserts the refusal: IsAccepted false, the exit code and the unsupported-option line.
- Actual: The helper's only assertion is CollectionAssert.DoesNotContain(... blankLine).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
