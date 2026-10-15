---
id: BL-2210
title: Fix AF-0208: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2210 — Fix AF-0208: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing

## Goal

The defect the audit office reported as AF-0208 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0208 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0208-handbuilttlsprovider-handshaketls13async-s-alongsi.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 left this mutant surviving: 'settings.ToTls13(alongsideTls12: false)' -> 'settings.ToTls13(alongsideTls12: true)'. The flag's only effect, in ToTls13 at line 814, decides the ClientHello's supported_groups for a TLS 1.3-minimum handshake: false drops every group TLS 1.3 cannot key-share (TlsNamedGroup.CanShare). The profiles' default lists hold only shareable groups, but --curves rewrites the list (CurvesAndSignatureAlgorithms.Apply). So with --tlsv1.3 and a TLS 1.2-only curve such as brainpoolP256r1 (0x001a), the hello would advertise a group it cannot use: different request bytes from the measured profile. AuthenticateAsClientAsync_WithATls13Minimum_OffersOnlyTls13InTheProfilesOrder checks suites, supported_versions and extension order, never supported_groups' content.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702:false -Member HandshakeTls13Async -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins the TLS 1.3-only hello's supported_groups under --curves.
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: new test `AuthenticateAsClientAsync_WithCurvesAndATls13Minimum_OffersOnlyTheGroupsTls13CanShare`
  (both builds) in `HandBuiltTlsProviderTests.CurvesAndSignatureAlgorithms.cs`: `--tlsv1.3` with
  `--curves brainpoolP256r1:X25519` must offer supported_groups and key_share `[x25519]` only.
  Expected bytes follow the documented OpenSSL rule in `ToTls13` (BL-1086); no product change.
- Touches widened to `Curl.Networking.UnitTests` (the test lives there); no task in Doing on
  `origin/work/dark-factory` names it.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit guard), so the
  mutant was applied by hand (`alongsideTls12: false` -> `true` at line 702): both new rows
  failed (`CollectionAssert.AreEqual failed. Element at index 0 do not match.`), then the line was
  restored. The quality auditor's re-audit runs the tool itself.
- The formatter hook also split a joined line in `ToTls12` (`FixedExtensions` / `InsertEmptyFragment`);
  whitespace only.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A test pins the TLS 1.3-only hello's supported_groups under --curves; AF-0208's mutant is killed
