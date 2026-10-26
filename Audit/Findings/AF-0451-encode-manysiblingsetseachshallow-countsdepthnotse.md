---
id: AF-0451
title: Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes checks only that some bytes came back, not the encoding
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:Encode_ManySiblingSetsEachShallow_CountsDepthNotSetsAndEncodes:weak-assertion
reproduction: none
task: BL-3528
tasks: BL-3528
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

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | LdapAdversarialTests.cs:382-386: asserts the first byte is 0xA0 and the AND header is 2-5 bytes, and runs CollectionAssert.AreEqual on the child encodings (the 11-byte OR of cn=a, repeated per sibling).

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
