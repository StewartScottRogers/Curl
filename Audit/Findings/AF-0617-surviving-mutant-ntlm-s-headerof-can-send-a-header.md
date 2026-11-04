---
id: AF-0617
title: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:HeaderOf-and:surviving-mutant
reproduction: mutation Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183:&&
task: none
tasks:
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0617 - Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing

## Summary

High finding from the quality auditor at `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`

Mutant 'step.Status is ContinueNeeded or Completed && step.Token.Length != 0' -> '... || step.Token.Length != 0' survived. With it, a ContinueNeeded/Completed step with an empty token sends 'NTLM ' with an empty value, and a Refused/MalformedToken step with a token sends that token instead of nothing. Either way the Authorization request bytes change; no test pins an empty-token step.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183:&& -Member HeaderOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
