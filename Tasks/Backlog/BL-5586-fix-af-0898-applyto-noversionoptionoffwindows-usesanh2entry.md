---
id: BL-5586
title: Fix AF-0898: ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry checks only that some route exists
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5586 — Fix AF-0898: ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry checks only that some route exists

## Goal

The defect the audit office reported as AF-0898 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0898 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:45`

Location: `Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:45`

The name promises the transfer uses the stored h2 entry for port 8443. The only assertion is Assert.IsNotNull(cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute); A route with the wrong ALPN, host or port passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs -Pattern 'Assert.IsNotNull\(cache.ApplyTo'
```

- Expected: No match: the test asserts the route's ALPN h2 and port 8443.
- Actual: AltSvcTransferCacheH3SwitchTests.cs:45: Assert.IsNotNull(cache.ApplyTo(...).AltSvcRoute);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
