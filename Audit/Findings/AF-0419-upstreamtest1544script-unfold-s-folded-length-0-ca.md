---
id: AF-0419
title: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-24.
key: quality:Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:Unfold-gt:surviving-mutant
reproduction: none
task: BL-4448
tasks: BL-3468, BL-4448
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0419 - UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space.

## Evidence

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'line = folded.Length > 0 ? folded + " " + line : line;' -> '>= 0'. With no pending fold, the line becomes " " + line. Its first character is then never '#', so comment lines are no longer dropped, and the option patterns the script matches see a shifted line. The script decides test1544's verdict, and no test notices the change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-24 | 2026-10-24_2106.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. Unfold was rewritten: it now always does line = folded + " " + line (UpstreamTest1544Script.cs:125, with a comment citing AF-0419) and has no '>' comparison left to mutate, so the mutant cannot be run. Not re-audited.
- 2026-10-25 | 2026-10-25_0941.md | not re-audited | Ran the -Site reproduction (UpstreamTest1544Script.cs:124:>, -Member Unfold): outcome site-missing, resolvedLine null, no mutant run. Unfold now always writes 'line = folded + " " + line;' with no folded.Length > 0 test, so the site is gone. Not re-audited.
- 2026-10-25 | 2026-10-25_1542.md | not re-audited | Ran the -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold command: outcome site-missing. Unfold no longer has the folded.Length > 0 test; line 124 now reads line = folded + " " + line; with a comment that WithoutComments trims each line. No mutant ran, so this is not re-audited.
- 2026-10-25 | 2026-10-25_1944.md | not re-audited | Ran Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold -ExcludeBaselineFailures -TimeoutSeconds 600: outcome site-missing (baseline 77555 ms, no mutants). grep finds no 'folded.Length' in UpstreamTest1544Script.cs any more; the code was rewritten, so the site could not be run. Not re-audited.
- 2026-10-29 | 2026-10-29_0606.md | not re-audited | Ran the reproduction (-Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold, run alone after an earlier parallel attempt's baseline failed under load). outcome: site-missing, resolvedLine null. Unfold (line 116) no longer has a `>` site: it now always writes `line = folded + " " + line;` (line 126), with a comment that WithoutComments trims every returned line (AF-0419). No mutant could run, so the finding was not re-audited.
- 2026-10-31 | 2026-10-31_0358.md | not re-audited | Ran the -Site reproduction: outcome site-missing (resolvedLine null; baseline excluded 3 failing Tls13ServerStreamTests). Unfold no longer has the folded.Length > 0 comparison: line 126 is now `line = folded + " " + line;` unconditionally, with a comment saying the leading space is harmless because WithoutComments trims each line. No mutant ran, so not re-audited.
- 2026-10-31 | 2026-10-31_1149.md | not re-audited | Ran the -Site command: baseline built and passed (3 Tls13ServerStreamTests excluded as unmutated failures), then 'site-missing Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:>' with resolvedLine null. Unfold (line 116) now always does 'line = folded + " " + line;' with a comment citing AF-0419, so there is no 'folded.Length > 0' comparison to mutate. Site missing, so not re-audited.
- 2026-11-01 | 2026-11-01_0144.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null (the baseline left out the already-failing UpstreamCase_RunThroughCurl_HoldsTheRatchet). The conditional is gone: Unfold now always does line = folded + " " + line (UpstreamTest1544Script.cs:126), and lines 124-125 are a comment saying the leading space is harmless because WithoutComments trims each line. No '>' site remains to mutate, so not re-audited.
- 2026-11-01 | 2026-11-01_1016.md | not re-audited | Ran the -Site reproduction: outcome site-missing, resolvedLine null, no mutant run. Unfold was rewritten: it now always does line = folded + " " + line; the folded.Length > 0 comparison no longer exists in the member, so the site cannot be mutated. Not re-audited.
- 2026-11-01 | 2026-11-01_1801.md | not re-audited | Ran powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold -ExcludeBaselineFailures -TimeoutSeconds 600: outcome site-missing, resolvedLine null (baseline excluded UpstreamCase_RunThroughCurl_HoldsTheRatchet). Unfold no longer has a folded.Length > 0 comparison: it now prepends folded + " " unconditionally with a comment that WithoutComments trims every line. Not re-audited.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-11-01: re-fix task BL-4448 filed (Stewart: re-fix them). The mutation site was removed by BL-3468's rewrite, so every re-audit since reported site-missing; reproduction set to none, so the next re-audit judges by reading the code and BL-4448's tests.
