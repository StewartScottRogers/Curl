---
id: AF-0442
title: ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks checks only that both callbacks are not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
key: quality:Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs:ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks:weak-assertion
reproduction: none
task: BL-3519
tasks: BL-3519
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_1542.md, 2026-10-25_1944.md
---
# AF-0442 - ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks checks only that both callbacks are not null

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs:113`: ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks checks only that both callbacks are not null. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs:113`

Body: 'EasyCallbackOptions callbacks = new CurlHstsStore().ApplyTo(new EasyCallbackOptions());' then 'Assert.IsNotNull(callbacks.HstsRead);' and 'Assert.IsNotNull(callbacks.HstsWrite);'. ApplyTo could set callbacks over another store, or ones that ignore the store, and this test would still pass. The other tests drive CreateReadCallback directly, not the callbacks ApplyTo sets. Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs -Pattern 'ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks' -Context 0,7
```

- Expected: The test drives the callbacks ApplyTo set and checks they read and write this store's entries.
- Actual: Only Assert.IsNotNull(callbacks.HstsRead) and Assert.IsNotNull(callbacks.HstsWrite).

## Re-audits

- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the Select-String reproduction. ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks now calls HstsRead (Ok with Forever, then Done with null) and HstsWrite (Ok, store holds Example) and asserts each result, not just IsNotNull.
- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | CurlHstsStoreTests.cs:109-121 now calls both callbacks and checks the results: HstsRead gives Ok with the Forever entry, then Done with null; HstsWrite gives Ok, and store.GetEntries() equals [Example].

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
