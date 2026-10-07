---
id: AF-0045
title: openssh-key-v1 RSA CRT exponent `d % (p - 1)` can become `d % (p + 1)` with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:FromComponents-MinusOneToPlusOne:surviving-mutant
task: none
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
closed:
closed-by:
---
# AF-0045 - openssh-key-v1 RSA CRT exponent `d % (p - 1)` can become `d % (p + 1)` with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99`: openssh-key-v1 RSA CRT exponent `d % (p - 1)` can become `d % (p + 1)` with no test failing. Reported by an auditor flagged unreliable in 2026-10-07_0844.md.

## Evidence

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0: 'writer.WriteInteger(d % (p - 1));' -> 'writer.WriteInteger(d % (p + 1));' survived. FromComponents computes the two CRT exponents that an openssh-key-v1 file leaves out, then imports the PKCS#1 key it builds. A wrong dP gives a wrong CRT private key, which signs the SSH publickey userauth wrongly wherever the platform's RSA trusts the CRT values. No test signs with an openssh-key-v1 RSA key and verifies the signature, or compares the derived exponent with d mod (p-1). Medium rather than High: OpenSSL is known to check a CRT result and fall back, so whether this reaches users depends on the platform.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-ssh.json
```

- Expected: The mutant at Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99 (- 1 to + 1) is killed.
- Actual: survived  Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs:99 -1

## Re-audits

## Log

- 2026-10-07: filed proposed.
