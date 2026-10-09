---
id: AF-0081
title: OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates checks only that the result is not null
auditor: quality
severity: Low
status: accepted
reason:
key: quality:Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs:OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates:weak-assertion
reproduction: none
task: BL-1759
tasks: BL-1759
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0081 - OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates checks only that the result is not null

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs:29`: OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates checks only that the result is not null.

## Evidence

Location: `Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs:29`

The only assertion is 'Assert.IsNotNull(certificates);'. The name promises the store's certificates, but an implementation that always returned an empty collection would pass. The test could compare the result with what X509Store(StoreName.My, location) itself lists.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs -Pattern 'Assert\.'
```

- Expected: An assertion comparing the returned certificates with the store's own contents.
- Actual: SystemClientCertificateStoreTests.cs:29: Assert.IsNotNull(certificates);

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Ran the Select-String: line 34 Assert.IsNotNull(actual) is now followed by line 35 CollectionAssert.AreEqual(expected, actual), where expected is the sorted thumbprints X509Store lists for the same location and store. The test checks the certificates themselves, not only non-null.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
