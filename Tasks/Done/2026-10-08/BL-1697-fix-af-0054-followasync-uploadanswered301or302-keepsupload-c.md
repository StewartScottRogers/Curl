---
id: BL-1697
title: Fix AF-0054: FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1697 — Fix AF-0054: FollowAsync_UploadAnswered301Or302_KeepsUpload checks only that the second request's Upload is not null

## Goal

The defect the audit office reported as AF-0054 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0054 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0054-followasync-uploadanswered301or302-keepsupload-che.md`.

Location: `Curl.Core.UnitTests/RedirectFollowerTests.cs:648`

Location: `Curl.Core.UnitTests/RedirectFollowerTests.cs:648`

The comment at line 642 says curl sends PUT /a and PUT /next, both with "abc". The only assertion is Assert.IsNotNull(handler.Contexts[1].Upload). A follower that switched the method to GET, or kept a non-null but rewound or empty upload stream, would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/RedirectFollowerTests.cs -Pattern 'KeepsUpload' -Context 0,9
```

- Expected: Assertions on the redirected request's method and upload bytes.
- Actual: Only Assert.IsNotNull(handler.Contexts[1].Upload);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now pins what curl sends on the second hop: no custom method (so `-T` keeps its implied PUT, not a switch to GET), `NoBody` false, two uploads sent, and the second upload is the bytes 1, 2, 3 read from the start - not just a non-null stream.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. KeepsUpload test now asserts the second hop's method, NoBody and upload bytes
