---
id: AF-0054
title: FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Core.UnitTests/RedirectFollowerTests.cs:FollowAsync_UploadAnswered301Or302_KeepsUpload:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0054 - FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null

## Summary

Low finding from the quality auditor at `Curl.Core.UnitTests/RedirectFollowerTests.cs:648`: FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null.

## Evidence

Location: `Curl.Core.UnitTests/RedirectFollowerTests.cs:648`

The comment at line 642 says curl sends PUT /a and PUT /next, both with "abc". The only assertion is Assert.IsNotNull(handler.Contexts[1].Upload). A follower that switched the method to GET, or kept a non-null but rewound or empty upload stream, would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/RedirectFollowerTests.cs -Pattern 'KeepsUpload' -Context 0,9
```

- Expected: Assertions on the redirected request's method and upload bytes.
- Actual: Only Assert.IsNotNull(handler.Contexts[1].Upload);

## Re-audits

## Log

- 2026-10-07: filed proposed.
