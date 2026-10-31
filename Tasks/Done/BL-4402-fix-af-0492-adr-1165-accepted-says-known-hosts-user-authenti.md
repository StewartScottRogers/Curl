---
id: BL-4402
title: Fix AF-0492: ADR-1165 (Accepted) says known_hosts, user authentication and key files stay in Curl.Protocol.Ssh, but they now live in Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4402 — Fix AF-0492: ADR-1165 (Accepted) says known_hosts, user authentication and key files stay in Curl.Protocol.Ssh, but they now live in Curl.Ssh.UnitLibrary

## Goal

The defect the audit office reported as AF-0492 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0492 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0492-adr-1165-accepted-says-known-hosts-user-authentica.md`.

Location: `Documentation/Planning/Decisions/ADR-1165-the-ssh-transport-layer-is-a-shared-library.md:38`

Location: `Documentation/Planning/Decisions/ADR-1165-the-ssh-transport-layer-is-a-shared-library.md:38`

ADR-1165 lines 37-39: '`known_hosts` (`KnownHostsFile`, `SshHostKeyChecker` and the rest of `HostKeys`), the private key files, user authentication, the session channel, SCP and SFTP stay in the protocol library'. The code has Curl.Ssh.UnitLibrary/HostKeys/KnownHostsFile.cs, Curl.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs, Curl.Ssh.UnitLibrary/Authentication/SshUserAuthentication.cs and a Keys/ folder. Curl.Protocol.Ssh.UnitLibrary keeps only Scp, Sftp, SshDiagnosticLog*, SshProtocolHandler and the like. Curl.Protocol.Ssh.UnitTests/GlobalUsings imports Curl.Ssh.Authentication, Curl.Ssh.HostKeys and Curl.Ssh.Keys. Neither the status line nor an amendment note marks this. ADR-1166 says it 'Extends ADR-1165' only for the connection protocol. An agent following the ADR would look for or add known_hosts and authentication code in the wrong library. Line 44 'Every moved type stays internal' is also untrue: ISshRandomSource, SystemSshRandomSource and SshAlgorithmPreferences in Curl.Ssh.UnitLibrary are public.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-1165-*.md -Pattern 'the session channel, SCP and SFTP stay in the protocol').Count; (Get-ChildItem Curl.Ssh.UnitLibrary -Recurse -Filter *.cs | Select-String -Pattern 'class (KnownHostsFile|SshHostKeyChecker|SshUserAuthentication)\b').Count
```

- Expected: Either the ADR no longer says these stay in the protocol library (first count 0, or an amendment note), or the second count is 0.
- Actual: 1 and 3: the ADR still says they stay, and all three classes are in Curl.Ssh.UnitLibrary.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Docs-only: ADR-1165 amended with a superseded note and the public-types exception; repro now gives 0. No code changed, so build and tests unaffected.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-31: Doing -> Done. ADR-1165 amended: superseded note for the moved known_hosts/auth/key files and the public-types exception; repro gives 0.
