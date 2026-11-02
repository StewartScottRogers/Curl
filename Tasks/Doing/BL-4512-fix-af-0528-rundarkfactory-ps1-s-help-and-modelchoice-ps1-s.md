---
id: BL-4512
title: Fix AF-0528: RunDarkFactory.ps1's help and ModelChoice.ps1's $OpusLibraries call their list the hand-built security and crypto libraries, but it omits Curl.Ssh.UnitLibrary, which holds SSH's key exchanges and ciphers
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-11-02
completed:
---
# BL-4512 — Fix AF-0528: RunDarkFactory.ps1's help and ModelChoice.ps1's $OpusLibraries call their list the hand-built security and crypto libraries, but it omits Curl.Ssh.UnitLibrary, which holds SSH's key exchanges and ciphers

## Goal

The defect the audit office reported as AF-0528 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0528 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0528-rundarkfactory-ps1-s-help-and-modelchoice-ps1-s-op.md`.

Location: `RunDarkFactory.ps1:117`

Location: `RunDarkFactory.ps1:117`

RunDarkFactory.ps1:115-118 (THE MODEL PER TASK): 'Otherwise opus when the task's touches name a hand-built security or crypto library (Curl.Cryptography, Curl.Tls, Curl.Quic, Curl.Kerberos, Curl.Ntlm, Curl.Original.Protocol.Ssh, or their .UnitTests or .IntegrationTests twin)'. DarkFactory/ModelChoice.ps1:8-9: '# The hand-built security and crypto libraries; a task touching one, or its test twin, runs on opus.' then $OpusLibraries = 'Curl.Cryptography', 'Curl.Tls', 'Curl.Quic', 'Curl.Kerberos', 'Curl.Ntlm', 'Curl.Original.Protocol.Ssh'. SSH's hand-built cryptography is no longer in Curl.Original.Protocol.Ssh.UnitLibrary, which now holds Scp/, Sftp/, SshProtocolHandler.cs and diagnostics. It is in Curl.Ssh.UnitLibrary: KeyExchange/ (Curve25519SshKeyExchange.cs, HybridKemSshKeyExchange.cs, MlKemSshKeyShare.cs, EcdhSshKeyExchange.cs, ...), PacketProtection/ (AesCtrSshCipher.cs), Keys/, HostKeys/. Matched against $OpusLibraryPattern, 'Curl.Ssh.UnitLibrary/PacketProtection/AesCtrSshCipher.cs' and 'Curl.Ssh.UnitLibrary/KeyExchange' give False while 'Curl.Original.Protocol.Ssh.UnitLibrary/Scp' gives True. So a crypto task outside the feature pipeline that touches only Curl.Ssh runs on sonnet, although the help and CLAUDE.md say security and crypto work runs on opus; the SSH hard-work marker applies only to feature tasks. The help's list is true of $OpusLibraries, but its claim that these are the hand-built security and crypto libraries is stale. Phase 2: ADR-1165 ('the SSH transport layer is a shared library', Accepted 2026-10-30) explains the move; neither list was updated with it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -Command ". ./DarkFactory/ModelChoice.ps1; 'Curl.Ssh.UnitLibrary/KeyExchange/Curve25519SshKeyExchange.cs' -match $OpusLibraryPattern"
```

- Expected: True: a task touching SSH's hand-built key exchange runs on opus, as the help's 'hand-built security or crypto library' rule says.
- Actual: False

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
