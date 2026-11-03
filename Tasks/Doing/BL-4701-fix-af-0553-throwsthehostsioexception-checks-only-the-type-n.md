---
id: BL-4701
title: Fix AF-0553: ThrowsTheHostsIOException checks only the type, not that it is the host's own exception
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4701 — Fix AF-0553: ThrowsTheHostsIOException checks only the type, not that it is the host's own exception

## Goal

The defect the audit office reported as AF-0553 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0553 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0553-throwsthehostsioexception-checks-only-the-type-not.md`.

Location: `Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs:35`

Location: `Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs:35`

The body is 'Assert.ThrowsExactly<IOException>(() => new ExtensionHelperProcessStarter(new StandInExtensionProcessStarter(null)).Start("p", "demo://h"));'. The stand-in throws new IOException("The system cannot find the file specified.") and nothing compares that message or instance, so a starter that threw its own IOException would pass. This is the same pattern as AF-0526.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs -Pattern 'ThrowsTheHostsIOException' -Context 0,2
```

- Expected: The thrown exception's message, or identity, is asserted.
- Actual: Only Assert.ThrowsExactly<IOException>(...).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
