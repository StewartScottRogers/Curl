---
id: AF-0537
title: Surviving mutant: Lib677.Exchange.RunAsync can carry on after a failed multi perform without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:RunAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102:false
task: BL-4643
tasks: BL-4643
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0537 - Surviving mutant: Lib677.Exchange.RunAsync can carry on after a failed multi perform without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102`: Surviving mutant: Lib677.Exchange.RunAsync can carry on after a failed multi perform without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102`

Mutant 'return false;' -> 'return true;' inside 'if (!(await steps.MultiPerformAsync(...)).GoesOn || !ReadMessages())' survived (Curl.Conformance.LibTest.UnitTests green). The caller then takes the 'loop ran to its end' branch: exchange.PrintReply() and multi.RemoveHandle(easy), where upstream lib677.c jumps to test_cleanup. The port's stdout (the reply line once the command was sent) and its handle cleanup differ from upstream after a failed curl_multi_perform or a done message without a socket. No test drives either failure through RunAsync.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102:false -Member RunAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
