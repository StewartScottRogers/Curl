---
id: BL-5215
title: Fix AF-0766: Surviving mutant: the DNS-over-TCP stream can be made not to own its socket, leaving it open
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5215 — Fix AF-0766: Surviving mutant: the DNS-over-TCP stream can be made not to own its socket, leaving it open

## Goal

The defect the audit office reported as AF-0766 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0766 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35`

Location: `Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35`

Mutant 'return new NetworkStream(socket, ownsSocket: true);' -> 'ownsSocket: false' survived. Disposing the stream would then leave the socket open: no FIN reaches the DNS server, and one descriptor leaks per TCP lookup until the GC runs. The member is [ExcludeFromCodeCoverage] (ADR-0083, a thin socket adapter measured by the Integration run), so no fast test can catch this decision.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/DnsSocketOpener.cs line 35` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/DnsSocketOpener.cs line 35`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
