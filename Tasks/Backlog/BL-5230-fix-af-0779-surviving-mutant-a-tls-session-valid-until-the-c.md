---
id: BL-5230
title: Fix AF-0779: Surviving mutant: a TLS session valid until the current second can be dropped as expired
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5230 — Fix AF-0779: Surviving mutant: a TLS session valid until the current second can be dropped as expired

## Goal

The defect the audit office reported as AF-0779 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0779 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TlsSessionCache.cs:337`

Location: `Curl.Networking.UnitLibrary/TlsSessionCache.cs:337`

peer.Sessions.RemoveAll(session => session.ValidUntil < now); mutated to <= survived (seed 68118 sample). The boundary of session expiry decides whether a session is resumed or a full handshake is made, and no test pins a session at its exact ValidUntil second.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/TlsSessionCache.cs line 337` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/TlsSessionCache.cs line 337`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
