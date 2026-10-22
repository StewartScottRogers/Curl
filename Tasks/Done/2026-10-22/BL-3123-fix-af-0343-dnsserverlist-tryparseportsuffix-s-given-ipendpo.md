---
id: BL-3123
title: Fix AF-0343: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3123 — Fix AF-0343: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused

## Goal

The defect the audit office reported as AF-0343 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0343 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0343-dnsserverlist-tryparseportsuffix-s-given-ipendpoin.md`.

Location: `Curl.Networking.UnitLibrary/DnsServerList.cs:113`

Location: `Curl.Networking.UnitLibrary/DnsServerList.cs:113`

Sampled mutant (seed 0) survived: 'given > IPEndPoint.MaxPort' became 'given >= IPEndPoint.MaxPort'. A --dns-servers entry naming port 65535 is valid, but the mutant refuses it, failing the option with an exit code instead of resolving. No Curl.Networking.UnitTests test pins the top port boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerList.cs:113:> -Member TryParsePortSuffix -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix, done directly rather than through the full feature pipeline: no production code changes. `DnsServerListTests` gains `192.0.2.1:65535` (accepted, kept as port 65535) and `1.2.3.4:65536` (refused), pinning both sides of `IPEndPoint.MaxPort`.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`given >= IPEndPoint.MaxPort`): `TryParse_OneEntry_ReturnsItsEndPoint ("192.0.2.1:65535")` failed (1 of 27 DnsServerListTests), so the mutant is killed; the change was then reverted. The re-audit runs the tool itself.
- Build clean, fast tests green (44 test projects, 0 failed). The test is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. --dns-servers port 65535 accepted and 65536 refused are pinned by DnsServerListTests, killing AF-0343's mutant
