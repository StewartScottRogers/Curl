---
id: BL-3528
title: Fix AF-0451: Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3528 — Fix AF-0451: Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding

## Goal

The defect the audit office reported as AF-0451 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0451 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0451-encode-manysiblingsetseachshallow-countsdepthnotse.md`.

Location: `Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:379`

Location: `Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:379`

The only assertion is Assert.IsNotNull(encoded) for a filter of 2*MaximumSetDepth sibling (|(cn=a)) sets. It shows the depth limit did not refuse the filter, but the name also says 'AndEncodes', and an encoder that returned the wrong bytes (for example one set dropped) would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs -Pattern 'Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes' -Context 0,7
```

- Expected: The test asserts the encoded bytes, or at least their length or set count.
- Actual: The only assertion is Assert.IsNotNull(encoded);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
