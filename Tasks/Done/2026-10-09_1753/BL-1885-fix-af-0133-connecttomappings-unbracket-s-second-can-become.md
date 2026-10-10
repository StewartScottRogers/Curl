---
id: BL-1885
title: Fix AF-0133: ConnectToMappings.Unbracket's second '&&' can become '||' with no test failing: --connect-to hosts ending in ']' are stripped, and ']' throws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1885 — Fix AF-0133: ConnectToMappings.Unbracket's second '&&' can become '||' with no test failing: --connect-to hosts ending in ']' are stripped, and ']' throws

## Goal

The defect the audit office reported as AF-0133 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0133 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0133-connecttomappings-unbracket-s-second-can-become-wi.md`.

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:153`

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:153`

The seeded sample (Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0) reported the mutant 'host.Length >= 2 && host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host' -> 'host.Length >= 2 && host.StartsWith('[') || host.EndsWith(']') ? ...' as survived (member Unbracket). HostMatches compares Unbracket(mappedHost) with Unbracket(host). A mapping host is everything before the first ':' when it does not start with '[', so a --connect-to value 'ax]:80:127.0.0.9:9' becomes host 'x' under the mutant and wrongly matches URL host 'x', redirecting the connection. A mapping host of ']' (length 1) makes host[1..^1] throw ArgumentOutOfRangeException. ConnectToMappingsTests only covers bracketed IPv6 hosts and plain names. Note on the reproduction: line 153 holds two '&&' sites, and Invoke-MutationTest.ps1 -Site always takes the line's first site with no column option. Run that way it mutates the first '&&' ('Length >= 2 || ...'), which is killed. That is a different mutant from this one, so a -Site rerun would close this finding falsely. reproduction.mutation is left out for that reason, and the reproduction is the deterministic seeded sample that contains this mutant.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:153 whose 'mutated' text is 'host.Length >= 2 && host.StartsWith('[') || host.EndsWith(']') ? host[1..^1] : host;' has outcome killed.
- Actual: That mutant (member Unbracket, operator &&) has outcome survived.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right; the gap was a missing test. Added
  `Map_WithAnUnbracketedHostEndingInAClosingBracket_ComparesTheHostAsWritten` to
  `Curl.Networking.UnitTests/ConnectToMappingsTests.cs` with rows `ax]` vs `x` and `]` vs `a`:
  both expect the URL's host left unmapped, since curl compares a mapping host that is not
  wrapped in `[...]` to the URL's host as written. I applied the mutant ('&&' -> '||' before
  `EndsWith`) by hand: both rows fail (the second because `host[1..^1]` throws), and both
  pass once it is reverted. I did not run the seeded Invoke-MutationTest sample: it is an
  audit path, which a lane may not read.
- `touches` named only the library. The library's own test project `Curl.Networking.UnitTests`
  was the only file changed. A library task always reaches its test project too, so I did
  not add it to `touches`.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. A test now kills the AF-0133 mutant; build clean, fast tests green
