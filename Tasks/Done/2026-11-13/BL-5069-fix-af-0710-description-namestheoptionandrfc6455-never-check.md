---
id: BL-5069
title: Fix AF-0710: Description_NamesTheOptionAndRfc6455 never checks that the description names RFC 6455
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketRepl.UnitTests]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5069 — Fix AF-0710: Description_NamesTheOptionAndRfc6455 never checks that the description names RFC 6455

## Goal

The defect the audit office reported as AF-0710 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0710 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0710-description-namestheoptionandrfc6455-never-checks.md`.

Location: `Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs:27`

Location: `Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs:27`

Step 1. The body has two assertions: Assert.AreEqual("ws-repl", WebSocketReplExtension.Description.Name) and Assert.AreSame(WebSocketReplExtension.Option, Description.Options.Single()). The description's specification link (WebSocketReplExtension.cs:29, new Uri("https://www.rfc-editor.org/rfc/rfc6455")) is never read, so dropping or changing the RFC 6455 reference would not fail the test its name promises.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs -Pattern 'Description_NamesTheOptionAndRfc6455' -Context 0,5
```

- Expected: The body asserts the description's RFC 6455 link (rfc6455).
- Actual: The body asserts only the name 'ws-repl' and the option; nothing mentions 6455.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added an assertion that Description.Specification equals the RFC 6455 URI. Project fast tests pass (58). Test-only change.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Test now asserts the RFC 6455 specification link
