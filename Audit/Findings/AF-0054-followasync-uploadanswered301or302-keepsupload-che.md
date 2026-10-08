---
id: AF-0054
title: FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
key: quality:Curl.Core.UnitTests/RedirectFollowerTests.cs:FollowAsync_UploadAnswered301Or302_KeepsUpload:weak-assertion
reproduction: none
task: BL-1697
tasks: BL-1697
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: reliable-reaudit
closed-by: 2026-10-08_0748.md
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

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the Select-String. FollowAsync_UploadAnswered301Or302_KeepsUpload (RedirectFollowerTests.cs:640-657) now asserts Assert.HasCount(2, handler.Uploads) and CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, handler.Uploads[1]), plus CustomMethod null and NoBody false.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
