---
id: BL-2659
title: Finish an upload at CURLOPT_INFILESIZE when the read callback never returns 0 (cases 1576 to 1580)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2645]
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2659 — Finish an upload at CURLOPT_INFILESIZE when the read callback never returns 0 (cases 1576 to 1580)

## Goal

A plain HTTP upload with `CURLOPT_INFILESIZE` 45 and a read callback that answers 45 bytes on every call finishes once 45 bytes are sent, as upstream's does, so upstream cases 1576, 1577 and 1579 pass (1578 and 1580 also need BL-2658).

## Context

Found by BL-2542, which ported lib1576. Cases 1576 to 1580 all end `curl did not finish within 20 seconds`: the read callback never returns 0, and the transfer keeps reading past `CURLOPT_INFILESIZE`. BL-2645 is the same symptom through a proxy tunnel; this one has no proxy and uses `CURLOPT_CUSTOMREQUEST "CURL"`, `CURLOPT_SEEKFUNCTION` and `Expect:` removed.

## Acceptance criteria

- [ ] Upstream cases 1576, 1577 and 1579 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Engine.UnitTests` covers stopping at the upload size (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer upload-size handling (InFileSize) and its existing InFileSize tests, so it can run beside other Engine tasks.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 3e71f510 35a2755a 3d836d5e.
