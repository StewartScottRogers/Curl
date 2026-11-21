---
id: BL-5673
title: Re-fix AF-0922: ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook asserts only that some hooks exist, not the DNS hook
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5673 — Re-fix AF-0922: ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook asserts only that some hooks exist, not the DNS hook

## Goal

The defect the audit office reported as AF-0922 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0922 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5657 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-21 | 2026-11-21_0114.md | reproduces: yes | Ran Select-String -Pattern 'ActivatesTheDnsHook' -Context 0,3 and read the test: CurlClient.ExtensionTransferHooksFor(values), the method the name is about, is still checked only by Assert.IsNotNull. The added AreEqual("DnsQueryTransferHook", ...) checks a separately built TransferHooks.ActivatedBy(...), not what CurlClient returned.

Location: `Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs:74`

Location: `Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs:74`

The test's only assertion is line 74 `Assert.IsNotNull(CurlClient.ExtensionTransferHooksFor(new Dictionary<string, string> { ["dns-json"] = string.Empty }));`. The name says --dns-json activates the DNS hook, but IsNotNull passes for any non-null hooks object, including one that activated a different extension's hook or none of DnsQuery's.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs -Pattern 'ActivatesTheDnsHook' -Context 0,3
```

- Expected: The test asserts the returned hooks contain the DnsQuery extension's hook (its type or its option).
- Actual: The only assertion is Assert.IsNotNull(CurlClient.ExtensionTransferHooksFor(...)).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
