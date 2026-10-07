---
id: AF-0011
title: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed
auditor: security
severity: Low
status: accepted
reason: 
key: security:Curl.Protocol.Ssh.UnitLibrary:ssh-parsers:unfuzzable
reproduction: none
task: BL-1267
tasks: BL-1267
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0011 - Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed

## Summary

Low finding from the security auditor at `Curl.Protocol.Ssh.UnitLibrary`: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed.

## Evidence

Location: `Curl.Protocol.Ssh.UnitLibrary`

dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 200000 --seed 863949067 printed 'ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes (public types: SshProtocolHandler, SshAlgorithmPreferences, ISshRandomSource, SystemSshRandomSource); nothing to fuzz.' and exited 2. Every SSH packet, key-exchange, authentication and SFTP reader is internal, so hostile-server bytes reach them untested by the fuzzer.

## Reproduction

Run from the repository root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out $env:TEMP\fuzz-ssh; $LASTEXITCODE
```

- Expected: The target fuzzes the SSH packet and message readers and exits 0 or 1.
- Actual: ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes ...; nothing to fuzz. Exit 2.

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | still reported
- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | Ran dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out <scratch>\fuzz-ssh-reaudit: printed 'ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes (public types: SshProtocolHandler, SshAlgorithmPreferences, ISshRandomSource, SystemSshRandomSource); nothing to fuzz.' and exited 2.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | still reported
- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | Ran dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out <scratch>\fuzz-ssh-reaudit: printed 'ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes (public types: SshProtocolHandler, SshAlgorithmPreferences, ISshRandomSource, SystemSshRandomSource); nothing to fuzz.' and exited 2. The library still offers no byte-level entry point.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
