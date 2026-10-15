---
id: AF-0208
title: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:HandshakeTls13Async-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702:false
task: BL-2210
tasks: BL-2210
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed: 2026-10-14
closed-how: mechanical
closed-by: 2026-10-14_1815.md
---
# AF-0208 - HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 left this mutant surviving: 'settings.ToTls13(alongsideTls12: false)' -> 'settings.ToTls13(alongsideTls12: true)'. The flag's only effect, in ToTls13 at line 814, decides the ClientHello's supported_groups for a TLS 1.3-minimum handshake: false drops every group TLS 1.3 cannot key-share (TlsNamedGroup.CanShare). The profiles' default lists hold only shareable groups, but --curves rewrites the list (CurvesAndSignatureAlgorithms.Apply). So with --tlsv1.3 and a TLS 1.2-only curve such as brainpoolP256r1 (0x001a), the hello would advertise a group it cannot use: different request bytes from the measured profile. AuthenticateAsClientAsync_WithATls13Minimum_OffersOnlyTls13InTheProfilesOrder checks suites, supported_versions and extension order, never supported_groups' content.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702:false -Member HandshakeTls13Async -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins the TLS 1.3-only hello's supported_groups under --curves.
- Actual: outcome survived

## Re-audits

- 2026-10-14 | 2026-10-14_1815.md | reproduces: no | Ran the targeted -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702:false -Member HandshakeTls13Async -ExcludeBaselineFailures -TimeoutSeconds 600. resolvedLine 702, mutated 'settings.ToTls13(alongsideTls12: true)', outcome killed (7600 ms). The 3 ECH tests that fail unmutated were excluded. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
