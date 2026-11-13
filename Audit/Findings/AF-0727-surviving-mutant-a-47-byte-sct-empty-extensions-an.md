---
id: AF-0727
title: Surviving mutant: a 47-byte SCT (empty extensions and signature) can be refused in the certificate text
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:FindSignatureStart-lt:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661:<
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0727 - Surviving mutant: a 47-byte SCT (empty extensions and signature) can be refused in the certificate text

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661`: Surviving mutant: a 47-byte SCT (empty extensions and signature) can be refused in the certificate text. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661`

Mutant 'timestamp.Length < 47' -> '<= 47' survived. The original formats the smallest well-formed v1 timestamp (43 bytes + 0 extensions + 4 bytes of algorithm and empty signature); the mutant throws AsnContentException for it, changing the -v certificate output. The minimum length is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661:< -Member FindSignatureStart -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
