---
id: BL-4714
title: Fix AF-0566: ADR-1216 says KnownFailures.txt lists WebTransport's failures BL-4633 and BL-4634; the file has no WebTransport line
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-03
completed:
---
# BL-4714 — Fix AF-0566: ADR-1216 says KnownFailures.txt lists WebTransport's failures BL-4633 and BL-4634; the file has no WebTransport line

## Goal

The defect the audit office reported as AF-0566 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0566 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0566-adr-1216-says-knownfailures-txt-lists-webtransport.md`.

Location: `Documentation/Planning/Decisions/ADR-1216-webtransport-conformance-pins-wt-enabled-and-exit-56-on-server-close.md:33`

Location: `Documentation/Planning/Decisions/ADR-1216-webtransport-conformance-pins-wt-enabled-and-exit-56-on-server-close.md:33`

Consequences: 'The client's present failures are listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`: every successful session ends in an `ObjectDisposedException` (BL-4633) and a server close is not read (BL-4634).' KnownFailures.txt has 4 entries (CurlSurface:FailWithBody-Gs-1 BL-4621, FailWithBody-S3-1 BL-4622, Verbose-Oci-1 BL-4623, FailWithBody-Oci-1 BL-4623) and no WebTransport, BL-4633 or BL-4634 line. The server close is read now: WebTransportSession.cs:56 throws RecvError 'The server closed the WebTransport session: code ..., ...'. The present-tense statement is false of the tree.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern 'WebTransport|BL-463[34]'
```

- Expected: Either lines naming BL-4633 and BL-4634, or an ADR that no longer says they are listed.
- Actual: No match; the file lists only BL-4621, BL-4622 and BL-4623 cases.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
