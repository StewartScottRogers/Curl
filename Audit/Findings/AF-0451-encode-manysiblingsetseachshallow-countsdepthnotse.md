---
id: AF-0451
title: Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0451 - Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding

## Summary

Low finding from the quality auditor at `Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:379`: Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:379`

The only assertion is Assert.IsNotNull(encoded) for a filter of 2*MaximumSetDepth sibling (|(cn=a)) sets. It shows the depth limit did not refuse the filter, but the name also says 'AndEncodes', and an encoder that returned the wrong bytes (for example one set dropped) would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs -Pattern 'Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes' -Context 0,7
```

- Expected: The test asserts the encoded bytes, or at least their length or set count.
- Actual: The only assertion is Assert.IsNotNull(encoded);

## Re-audits

## Log

- 2026-10-25: filed proposed.
