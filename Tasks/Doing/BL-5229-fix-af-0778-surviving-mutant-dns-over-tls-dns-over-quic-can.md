---
id: BL-5229
title: Fix AF-0778: Surviving mutant: --dns-over-tls/--dns-over-quic can refuse a name of exactly 255 octets
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.EncryptedDns.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5229 — Fix AF-0778: Surviving mutant: --dns-over-tls/--dns-over-quic can refuse a name of exactly 255 octets

## Goal

The defect the audit office reported as AF-0778 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0778 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62`

Location: `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62`

return name.Count > MaximumNameLength ? throw ... : [.. name]; with MaximumNameLength = 255; mutated to >= survived. RFC 1035 section 2.3.4 allows 255 octets. With the mutant, a maximum-length name is refused, so it resolves to nothing and the transfer fails with exit 6. Encode_Rfc1035Section2_3_4_NameOver255Octets_IsRefused uses four 63-octet labels (257 octets), so the boundary is never tested.

Reproduction: the audit office's mutation test changed `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs line 62` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs line 62`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: e52d72c8 9742157d.
