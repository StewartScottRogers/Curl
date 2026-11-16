---
id: BL-5299
title: Fix AF-0825: PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes checks only how many bytes were delivered, never which
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/Cgr126Tests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5299 — Fix AF-0825: PerformAsync_GzipBodyUnderAcceptEncoding_DeliversTheDecodedBytes checks only how many bytes were delivered, never which

## Goal

The defect the audit office reported as AF-0825 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0825 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/Cgr126Tests.cs:25`

Location: `Curl.Engine.UnitTests/Cgr126Tests.cs:25`

Step 1 (Engine sample). The name promises the decoded bytes, but the helper's write callback discards them (line 70: Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length }) and the test's only assertions are line 32 Assert.AreEqual(compressed.Length, easy.Info.SizeDownload) and line 33 Assert.AreEqual(5L, easy.Info.SizeDelivered). A gzip decoder that handed the write callback any five wrong bytes in place of 'hello' would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/Cgr126Tests.cs -SimpleMatch -Pattern "Write = bytes => bytes.Length"
```

- Expected: The test records the delivered bytes and asserts they are 'hello'.
- Actual: Cgr126Tests.cs:70: Callbacks = new EasyCallbackOptions { Write = bytes => bytes.Length }, and only SizeDownload and SizeDelivered are asserted

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
