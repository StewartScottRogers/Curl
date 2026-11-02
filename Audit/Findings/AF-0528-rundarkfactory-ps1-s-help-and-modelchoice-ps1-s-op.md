---
id: AF-0528
title: RunDarkFactory.ps1's help and ModelChoice.ps1's $OpusLibraries call their list the hand-built security and crypto libraries, but it omits Curl.Ssh.UnitLibrary, which holds SSH's key exchanges and ciphers
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:RunDarkFactory.ps1:OpusLibraries:false-help
reproduction: none
task: BL-4512
tasks: BL-4512
found: 2026-11-02
found-at: 4698726450f0ce3447bd6790d0bf9c59787bae95
scorecard: 2026-11-02_0645.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0528 - RunDarkFactory.ps1's help and ModelChoice.ps1's $OpusLibraries call their list the hand-built security and crypto libraries, but it omits Curl.Ssh.UnitLibrary, which holds SSH's key exchanges and ciphers

## Summary

Medium finding from the truthfulness auditor at `RunDarkFactory.ps1:117`: RunDarkFactory.ps1's help and ModelChoice.ps1's $OpusLibraries call their list the hand-built security and crypto libraries, but it omits Curl.Ssh.UnitLibrary, which holds SSH's key exchanges and ciphers. Reported by an auditor flagged unreliable in 2026-11-02_0645.md.

## Evidence

Location: `RunDarkFactory.ps1:117`

RunDarkFactory.ps1:115-118 (THE MODEL PER TASK): 'Otherwise opus when the task's touches name a hand-built security or crypto library (Curl.Cryptography, Curl.Tls, Curl.Quic, Curl.Kerberos, Curl.Ntlm, Curl.Original.Protocol.Ssh, or their .UnitTests or .IntegrationTests twin)'. DarkFactory/ModelChoice.ps1:8-9: '# The hand-built security and crypto libraries; a task touching one, or its test twin, runs on opus.' then $OpusLibraries = 'Curl.Cryptography', 'Curl.Tls', 'Curl.Quic', 'Curl.Kerberos', 'Curl.Ntlm', 'Curl.Original.Protocol.Ssh'. SSH's hand-built cryptography is no longer in Curl.Original.Protocol.Ssh.UnitLibrary, which now holds Scp/, Sftp/, SshProtocolHandler.cs and diagnostics. It is in Curl.Ssh.UnitLibrary: KeyExchange/ (Curve25519SshKeyExchange.cs, HybridKemSshKeyExchange.cs, MlKemSshKeyShare.cs, EcdhSshKeyExchange.cs, ...), PacketProtection/ (AesCtrSshCipher.cs), Keys/, HostKeys/. Matched against $OpusLibraryPattern, 'Curl.Ssh.UnitLibrary/PacketProtection/AesCtrSshCipher.cs' and 'Curl.Ssh.UnitLibrary/KeyExchange' give False while 'Curl.Original.Protocol.Ssh.UnitLibrary/Scp' gives True. So a crypto task outside the feature pipeline that touches only Curl.Ssh runs on sonnet, although the help and CLAUDE.md say security and crypto work runs on opus; the SSH hard-work marker applies only to feature tasks. The help's list is true of $OpusLibraries, but its claim that these are the hand-built security and crypto libraries is stale. Phase 2: ADR-1165 ('the SSH transport layer is a shared library', Accepted 2026-10-30) explains the move; neither list was updated with it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -Command ". ./DarkFactory/ModelChoice.ps1; 'Curl.Ssh.UnitLibrary/KeyExchange/Curve25519SshKeyExchange.cs' -match $OpusLibraryPattern"
```

- Expected: True: a task touching SSH's hand-built key exchange runs on opus, as the help's 'hand-built security or crypto library' rule says.
- Actual: False

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
