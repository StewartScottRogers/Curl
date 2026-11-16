---
id: BL-5272
title: Fix AF-0818: Surviving mutant: an AES-GCM ODoH response with a bad tag can be reported as opened
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5272 — Fix AF-0818: Surviving mutant: an AES-GCM ODoH response with a bad tag can be reported as opened

## Goal

The defect the audit office reported as AF-0818 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0818 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46`

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46`

In TryOpen, `return false;` after AuthenticationTagMismatchException changed to `return true;` survived. OpenResponse_ATamperedResponse_GivesNull (default suite Aes128Gcm) still passes, because a later check of the zeroed plaintext happens to reject it. TryOpen's own authentication decision is untested: nothing asserts that TryOpen refuses a tampered tag.

Reproduction: the audit office's mutation test changed `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs line 46` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs line 46`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
