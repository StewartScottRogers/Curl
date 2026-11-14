---
id: BL-5180
title: Fix AF-0735: HookFor_GraphQlIntrospectWithJsonRpc_Refuses checks only that a hook exists, which a single-option hook also satisfies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-5180 — Fix AF-0735: HookFor_GraphQlIntrospectWithJsonRpc_Refuses checks only that a hook exists, which a single-option hook also satisfies

## Goal

The defect the audit office reported as AF-0735 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0735 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs:35`

Location: `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs:35`

The only assertion is Assert.IsNotNull(BodyReplacingHookExclusivity.HookFor(Given(graphql-introspect, jsonrpc))). HookFor (BodyReplacingHookExclusivity.cs:26-36) returns null for no option, an UploadAndHeadRefusingHook for one and a RefusingHook naming both for two. If --graphql-introspect were not counted as --graphql's body, only jsonrpc would be given and HookFor would still return a non-null UploadAndHeadRefusingHook, which does not refuse the transfer; the test would pass. The neighbouring HookFor_TwoBodyReplacingOptions test shows the exact check (ExitCode FailedInit and the --a and --b cannot be used together message).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs -Pattern 'HookFor_GraphQlIntrospectWithJsonRpc_Refuses' -Context 0,3
```

- Expected: The test runs the hook and asserts exit 2 and the message --graphql and --jsonrpc cannot be used together.
- Actual: The body is the single line Assert.IsNotNull(BodyReplacingHookExclusivity.HookFor(Given("graphql-introspect", "jsonrpc")));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
