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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
