---
id: BL-1267
title: Fix AF-0011: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1267 — Fix AF-0011: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed

## Goal

The defect the audit office reported as AF-0011 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0011 (Low, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0011-curl-protocol-ssh-unitlibrary-exposes-no-byte-leve.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary`

Location: `Curl.Protocol.Ssh.UnitLibrary`

dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 200000 --seed 863949067 printed 'ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes (public types: SshProtocolHandler, SshAlgorithmPreferences, ISshRandomSource, SystemSshRandomSource); nothing to fuzz.' and exited 2. Every SSH packet, key-exchange, authentication and SFTP reader is internal, so hostile-server bytes reach them untested by the fuzzer.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out $env:TEMP\fuzz-ssh; $LASTEXITCODE
```

- Expected: The target fuzzes the SSH packet and message readers and exits 0 or 1.
- Actual: ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes ...; nothing to fuzz. Exit 2.

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
