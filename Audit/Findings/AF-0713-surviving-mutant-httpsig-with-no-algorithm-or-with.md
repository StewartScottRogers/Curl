---
id: AF-0713
title: Surviving mutant: --httpsig with no algorithm, or with ed25519, can crash or be refused
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:AlgorithmOf-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62:||
task: BL-5072
tasks: BL-5072
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0713 - Surviving mutant: --httpsig with no algorithm, or with ed25519, can crash or be refused

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62`: Surviving mutant: --httpsig with no algorithm, or with ed25519, can crash or be refused. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62`

Mutant 'given is null || given.Equals(Ed25519, ...)' -> 'given is null && given.Equals(...)' survived Curl.Engine.UnitTests. With no algorithm given the mutant falls through to given.Equals(HmacSha256...) on null (NullReferenceException), and an explicit 'ed25519' maps to null (unsupported). That breaks the default --httpsig path that decides the signature header bytes and the exit code, and no Engine test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62:|| -Member AlgorithmOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (given is null || given.Equals(Ed25519) -> &&). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
