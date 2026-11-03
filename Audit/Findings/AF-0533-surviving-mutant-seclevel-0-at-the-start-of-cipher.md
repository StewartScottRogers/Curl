---
id: AF-0533
title: Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:LowersSecurityLevelToZero-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201:>=
task: BL-4692
tasks: BL-4639, BL-4692
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0533 - Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201`: Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201`

Mutant 'return at >= 0 && ...StartsWith("0")' -> 'at > 0' survived (Curl.Networking.UnitTests green). A cipher string that begins with '@SECLEVEL=0' (at == 0, as in --ciphers '@SECLEVEL=0:...') would no longer lower the security level to zero. That is the decision that lets the OpenSSL-build hand-built client offer legacy TLS 1.0/1.1, so a handshake curl completes would fail with an SSL exit code. Every test puts the command after another token.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201:>= -Member LowersSecurityLevelToZero -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived at resolvedLine 201 ('return at >= 0 && ...' -> 'return at > 0 && ...'); the baseline left out three failing ECH tests. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
