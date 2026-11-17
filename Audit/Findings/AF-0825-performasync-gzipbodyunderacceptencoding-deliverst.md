---
id: AF-0825
title: PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes checks only how many bytes were delivered, never which
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitTests/Cgr126Tests.cs:PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0825 - PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes checks only how many bytes were delivered, never which

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/Cgr126Tests.cs:25`: PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes checks only how many bytes were delivered, never which. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Engine.UnitTests/Cgr126Tests.cs:25`

Step 1 (Engine sample). The name promises the decoded bytes, but the helper's write callback discards them (line 70: Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length }) and the test's only assertions are line 32 Assert.AreEqual(compressed.Length, easy.Info.SizeDownload) and line 33 Assert.AreEqual(5L, easy.Info.SizeDelivered). A gzip decoder that handed the write callback any five wrong bytes in place of 'hello' would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/Cgr126Tests.cs -SimpleMatch -Pattern "Write = bytes => bytes.Length"
```

- Expected: The test records the delivered bytes and asserts they are 'hello'.
- Actual: Cgr126Tests.cs:70: Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length }, and only SizeDownload and SizeDelivered are asserted

## Re-audits

## Log

- 2026-11-16: filed proposed.
