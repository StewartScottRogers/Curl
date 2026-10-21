---
id: BL-1672
title: Fix AF-0045: openssh-key-v1 RSA CRT exponent `d % (p - 1)` can become `d % (p + 1)` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1672 — Fix AF-0045: openssh-key-v1 RSA CRT exponent `d % (p - 1)` can become `d % (p + 1)` with no test failing

## Goal

The defect the audit office reported as AF-0045 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0045 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0045-openssh-key-v1-rsa-crt-exponent-d-p-1-can-become-d.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99`

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0: 'writer.WriteInteger(d % (p - 1));' -> 'writer.WriteInteger(d % (p + 1));' survived. FromComponents computes the two CRT exponents that an openssh-key-v1 file leaves out, then imports the PKCS#1 key it builds. A wrong dP gives a wrong CRT private key, which signs the SSH publickey userauth wrongly wherever the platform's RSA trusts the CRT values. No test signs with an openssh-key-v1 RSA key and verifies the signature, or compares the derived exponent with d mod (p-1). Medium rather than High: OpenSSL is known to check a CRT result and fall back, so whether this reaches users depends on the platform.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99:-1 -Member FromComponents -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99 (- 1 to + 1) is killed.
- Actual: survived  Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99 -1

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured first: a test that builds the test RSA key from its six openssh-key-v1
  integers and checks its `rsa-sha2-256` signature (or the exported `DP`) does NOT kill
  the mutant on Windows - CNG recomputes dP and dQ on import, so the wrong exponent never
  reaches a signature there. The defect still matters on platforms that trust the CRT values.
- So `RsaSshPrivateKey` now keeps `Pkcs1Encoding`, the DER it was imported from, and
  `FromComponents_TheSixOpenSshIntegers_DerivesDpAsDModPMinusOneAndDqAsDModQMinusOne`
  compares it byte for byte with the full key's `ExportRSAPrivateKey()`. Also added
  `FromComponents_TheSixOpenSshIntegers_SignsAsTheFullPkcs1Key` (end to end).
- Line 99 (`d % (p - 1)`) stays at line 99 inside `FromComponents`, so the finding's
  `-Site ...:99:-1 -Member FromComponents` still names it. Verified by hand: with the
  line mutated to `p + 1` the new test fails ("Element at index 3 do not match"); restored,
  all 1809 Ssh tests pass. A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`
  (audit path guard), so the scripted reproduction is left to the re-audit.
- Measure-CodeQuality not run (time budget): the change adds one auto-property and a
  constructor argument, both reached by the new tests.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A test now pins the openssh-key-v1 RSA CRT exponents FromComponents derives, killing the d % (p + 1) mutant
