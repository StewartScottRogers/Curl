---
id: AF-0735
title: HookFor_GraphQlIntrospectWithJsonRpc_Refuses checks only that a hook exists, which a single-option hook also satisfies
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs:HookFor_GraphQlIntrospectWithJsonRpc_Refuses:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0735 - HookFor_GraphQlIntrospectWithJsonRpc_Refuses checks only that a hook exists, which a single-option hook also satisfies

## Summary

Medium finding from the quality auditor at `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs:35`: HookFor_GraphQlIntrospectWithJsonRpc_Refuses checks only that a hook exists, which a single-option hook also satisfies.

## Evidence

Location: `Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs:35`

The only assertion is Assert.IsNotNull(BodyReplacingHookExclusivity.HookFor(Given(graphql-introspect, jsonrpc))). HookFor (BodyReplacingHookExclusivity.cs:26-36) returns null for no option, an UploadAndHeadRefusingHook for one and a RefusingHook naming both for two. If --graphql-introspect were not counted as --graphql's body, only jsonrpc would be given and HookFor would still return a non-null UploadAndHeadRefusingHook, which does not refuse the transfer; the test would pass. The neighbouring HookFor_TwoBodyReplacingOptions test shows the exact check (ExitCode FailedInit and the --a and --b cannot be used together message).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitTests/BodyReplacingHookExclusivityTests.cs -Pattern 'HookFor_GraphQlIntrospectWithJsonRpc_Refuses' -Context 0,3
```

- Expected: The test runs the hook and asserts exit 2 and the message --graphql and --jsonrpc cannot be used together.
- Actual: The body is the single line Assert.IsNotNull(BodyReplacingHookExclusivity.HookFor(Given("graphql-introspect", "jsonrpc")));

## Re-audits

## Log

- 2026-11-13: filed proposed.
