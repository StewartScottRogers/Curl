---
id: AF-0492
title: ADR-1165 (Accepted) says known_hosts, user authentication and key files stay in Curl.Protocol.Ssh, but they now live in Curl.Ssh.UnitLibrary
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-1165-the-ssh-transport-layer-is-a-shared-library.md:known_hosts-and-authentication-stay-in-protocol-library:stale-adr
reproduction: none
task: BL-4402
tasks: BL-4402
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0492 - ADR-1165 (Accepted) says known_hosts, user authentication and key files stay in Curl.Protocol.Ssh, but they now live in Curl.Ssh.UnitLibrary

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1165-the-ssh-transport-layer-is-a-shared-library.md:38`: ADR-1165 (Accepted) says known_hosts, user authentication and key files stay in Curl.Protocol.Ssh, but they now live in Curl.Ssh.UnitLibrary. Reported by an auditor flagged unreliable in 2026-10-31_0358.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1165-the-ssh-transport-layer-is-a-shared-library.md:38`

ADR-1165 lines 37-39: '`known_hosts` (`KnownHostsFile`, `SshHostKeyChecker` and the rest of `HostKeys`), the private key files, user authentication, the session channel, SCP and SFTP stay in the protocol library'. The code has Curl.Ssh.UnitLibrary/HostKeys/KnownHostsFile.cs, Curl.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs, Curl.Ssh.UnitLibrary/Authentication/SshUserAuthentication.cs and a Keys/ folder. Curl.Protocol.Ssh.UnitLibrary keeps only Scp, Sftp, SshDiagnosticLog*, SshProtocolHandler and the like. Curl.Protocol.Ssh.UnitTests/GlobalUsings imports Curl.Ssh.Authentication, Curl.Ssh.HostKeys and Curl.Ssh.Keys. Neither the status line nor an amendment note marks this. ADR-1166 says it 'Extends ADR-1165' only for the connection protocol. An agent following the ADR would look for or add known_hosts and authentication code in the wrong library. Line 44 'Every moved type stays internal' is also untrue: ISshRandomSource, SystemSshRandomSource and SshAlgorithmPreferences in Curl.Ssh.UnitLibrary are public.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-1165-*.md -Pattern 'the session channel, SCP and SFTP stay in the protocol').Count; (Get-ChildItem Curl.Ssh.UnitLibrary -Recurse -Filter *.cs | Select-String -Pattern 'class (KnownHostsFile|SshHostKeyChecker|SshUserAuthentication)\b').Count
```

- Expected: Either the ADR no longer says these stay in the protocol library (first count 0, or an amendment note), or the second count is 0.
- Actual: 1 and 3: the ADR still says they stay, and all three classes are in Curl.Ssh.UnitLibrary.

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
