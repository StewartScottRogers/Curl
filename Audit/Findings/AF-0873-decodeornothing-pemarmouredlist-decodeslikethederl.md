---
id: AF-0873
title: DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList only checks both decodes are non-null, never that they match
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs:DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList:weak-assertion
reproduction: none
task: BL-5537
tasks: BL-5537
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0602.md
---
# AF-0873 - DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList only checks both decodes are non-null, never that they match

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs:16`: DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList only checks both decodes are non-null, never that they match.

## Evidence

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs:16`

The test's only assertions are 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));' and 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(der));'. The name promises the PEM-armoured list decodes like the DER list, but the two results are never compared. A PEM path that decoded a different, empty or truncated list would pass. Step 0 candidate kind weak-assertion, only Assert.IsNotNull.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs -SimpleMatch -Pattern 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));'
```

- Expected: No match: the test compares the PEM decode with the DER decode (issuer, entries, next update).
- Actual: ServerCertificateVerificationTests.PemRevocationList.cs:16:        Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));

## Re-audits

- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the Select-String reproduction: no match. DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList now asserts Assert.AreEqual(fromDer.ThisUpdate, fromPem.ThisUpdate) and Assert.AreEqual(fromDer.NextUpdate, fromPem.NextUpdate) (lines 21 and 23).

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces.
