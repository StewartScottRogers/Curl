---
id: AF-0566
title: ADR-1216 says KnownFailures.txt lists WebTransport's failures BL-4633 and BL-4634; the file has no WebTransport line
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-1216-webtransport-conformance-pins-wt-enabled-and-exit-56-on-server-close.md:KnownFailures-listing:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0566 - ADR-1216 says KnownFailures.txt lists WebTransport's failures BL-4633 and BL-4634; the file has no WebTransport line

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1216-webtransport-conformance-pins-wt-enabled-and-exit-56-on-server-close.md:33`: ADR-1216 says KnownFailures.txt lists WebTransport's failures BL-4633 and BL-4634; the file has no WebTransport line. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1216-webtransport-conformance-pins-wt-enabled-and-exit-56-on-server-close.md:33`

Consequences: 'The client's present failures are listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`: every successful session ends in an `ObjectDisposedException` (BL-4633) and a server close is not read (BL-4634).' KnownFailures.txt has 4 entries (CurlSurface:FailWithBody-Gs-1 BL-4621, FailWithBody-S3-1 BL-4622, Verbose-Oci-1 BL-4623, FailWithBody-Oci-1 BL-4623) and no WebTransport, BL-4633 or BL-4634 line. The server close is read now: WebTransportSession.cs:56 throws RecvError 'The server closed the WebTransport session: code ..., ...'. The present-tense statement is false of the tree.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern 'WebTransport|BL-463[34]'
```

- Expected: Either lines naming BL-4633 and BL-4634, or an ADR that no longer says they are listed.
- Actual: No match; the file lists only BL-4621, BL-4622 and BL-4623 cases.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
