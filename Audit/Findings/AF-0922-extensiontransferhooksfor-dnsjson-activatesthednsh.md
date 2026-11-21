---
id: AF-0922
title: ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook asserts only that some hooks exist, not the DNS hook
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs:ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook:weak-assertion
reproduction: none
task: BL-5673
tasks: BL-5657, BL-5673
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0922 - ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook asserts only that some hooks exist, not the DNS hook

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs:74`: ExtensionTransferHooksFor_DnsJson_ActivatesTheDnsHook asserts only that some hooks exist, not the DNS hook.

## Evidence

Location: `Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs:74`

The test's only assertion is line 74 `Assert.IsNotNull(CurlClient.ExtensionTransferHooksFor(new Dictionary<string, string> { ["dns-json"] = string.Empty }));`. The name says --dns-json activates the DNS hook, but IsNotNull passes for any non-null hooks object, including one that activated a different extension's hook or none of DnsQuery's.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Composition/CurlClientCreateExtensionTests.cs -Pattern 'ActivatesTheDnsHook' -Context 0,3
```

- Expected: The test asserts the returned hooks contain the DnsQuery extension's hook (its type or its option).
- Actual: The only assertion is Assert.IsNotNull(CurlClient.ExtensionTransferHooksFor(...)).

## Re-audits

- 2026-11-21 | 2026-11-21_0114.md | reproduces: yes | Ran Select-String -Pattern 'ActivatesTheDnsHook' -Context 0,3 and read the test: CurlClient.ExtensionTransferHooksFor(values), the method the name is about, is still checked only by Assert.IsNotNull. The added AreEqual("DnsQueryTransferHook", ...) checks a separately built TransferHooks.ActivatedBy(...), not what CurlClient returned.
- 2026-11-21 | 2026-11-21_0835.md | reproduces: no | Ran the Select-String reproduction. The test now asserts the hook itself: line 86 Assert.AreEqual("DnsQueryTransferHook", HooksOf(wrapped)[0].GetType().Name) and line 89 the same of TransferHooks.ActivatedBy, beside the exact count 1.

## Log

- 2026-11-20: filed proposed.
