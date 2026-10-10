---
id: AF-0133
title: ConnectToMappings.Unbracket's second '&&' can become '||' with no test failing: --connect-to hosts ending in ']' are stripped, and ']' throws
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/ConnectToMappings.cs:Unbracket-and:surviving-mutant
reproduction: none
task: BL-1885
tasks: BL-1885
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0133 - ConnectToMappings.Unbracket's second '&&' can become '||' with no test failing: --connect-to hosts ending in ']' are stripped, and ']' throws

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectToMappings.cs:153`: ConnectToMappings.Unbracket's second '&&' can become '||' with no test failing: --connect-to hosts ending in ']' are stripped, and ']' throws. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:153`

The seeded sample (Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0) reported the mutant 'host.Length >= 2 && host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host' -> 'host.Length >= 2 && host.StartsWith('[') || host.EndsWith(']') ? ...' as survived (member Unbracket). HostMatches compares Unbracket(mappedHost) with Unbracket(host). A mapping host is everything before the first ':' when it does not start with '[', so a --connect-to value 'ax]:80:127.0.0.9:9' becomes host 'x' under the mutant and wrongly matches URL host 'x', redirecting the connection. A mapping host of ']' (length 1) makes host[1..^1] throw ArgumentOutOfRangeException. ConnectToMappingsTests only covers bracketed IPv6 hosts and plain names. Note on the reproduction: line 153 holds two '&&' sites, and Invoke-MutationTest.ps1 -Site always takes the line's first site with no column option. Run that way it mutates the first '&&' ('Length >= 2 || ...'), which is killed. That is a different mutant from this one, so a -Site rerun would close this finding falsely. reproduction.mutation is left out for that reason, and the reproduction is the deterministic seeded sample that contains this mutant.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:153 whose 'mutated' text is 'host.Length >= 2 && host.StartsWith('[') || host.EndsWith(']') ? host[1..^1] : host;' has outcome killed.
- Actual: That mutant (member Unbracket, operator &&) has outcome survived.

## Re-audits

- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction (-Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures -TimeoutSeconds 600). The sample included ConnectToMappings.cs:153 [Unbracket] 'host.StartsWith('[') && host.EndsWith(']')' -> '||' (the second &&), outcome killed.
- 2026-10-10 | 2026-10-10_0123.md | not re-audited | Ran the reproduction (sampled -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0): score 0.9211, but the sample held no Unbracket site at ConnectToMappings.cs:153. The only ConnectToMappings mutant was DestinationWhenMatching line 72 (killed). The old sampled command cannot tell, and -Site can only target the line's first '&&', not the second one the finding names.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
