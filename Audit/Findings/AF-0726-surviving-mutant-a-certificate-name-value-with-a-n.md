---
id: AF-0726
title: Surviving mutant: a certificate name value with a non-universal or constructed tag can print as text instead of #hex
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:AppendValue-or:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163:||
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
# AF-0726 - Surviving mutant: a certificate name value with a non-universal or constructed tag can print as text instead of #hex

## Summary

High finding from the quality auditor at `Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163`: Surviving mutant: a certificate name value with a non-universal or constructed tag can print as text instead of #hex. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163`

Mutant 'TagClass != Universal || IsConstructed || ...' -> 'TagClass != Universal && IsConstructed || ...' survived. A context-specific primitive tag numbered like a string type (12, 19, 22, ...) or a constructed universal string tag is dumped as '#<hex>' by the original and printed as characters by the mutant. That changes the -v certificate subject and issuer lines for such a server certificate. Format_ConstructedValue_DumpsTheDerInHex uses a SEQUENCE (tag 16, not in CharacterWidths), so it cannot tell.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163:|| -Member AppendValue -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
