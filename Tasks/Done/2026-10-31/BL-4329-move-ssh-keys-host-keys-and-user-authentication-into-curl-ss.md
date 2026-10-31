---
id: BL-4329
title: Move SSH keys, host keys and user authentication into Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4365]
touches: [Curl.Ssh.UnitLibrary/Keys, Curl.Ssh.UnitLibrary/HostKeys, Curl.Ssh.UnitLibrary/Authentication, Curl.Ssh.UnitTests/Keys, Curl.Ssh.UnitTests/HostKeys, Curl.Ssh.UnitTests/Authentication, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/Keys, Curl.Protocol.Ssh.UnitTests/HostKeys, Curl.Protocol.Ssh.UnitTests/Authentication, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Keys.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.HostKeys.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Authentication.cs, Curl.Protocol.Ssh.UnitTests/Fakes, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Ssh.Keys.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Ssh.Authentication.cs, Curl.Protocol.Ssh.UnitTests/SshDiagnosticLogTests.cs, Curl.Ssh.UnitLibrary/SshInfoLines.cs, Curl.Ssh.UnitLibrary/SshStateTrace.cs, Curl.Ssh.UnitLibrary/Curl.Ssh.UnitLibrary.csproj, Curl.Ssh.UnitTests/Fakes, Curl.Ssh.UnitTests/Transport/SshTransportTests.DelayedCompression.cs, Curl.Protocol.Ssh.IntegrationTests/Authentication, Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj, Curl.Console.UnitTests/CurlCompositionSshVerboseTests.cs, Curl.Conformance.SshServer.UnitLibrary/SshServerUserAuthentication.cs, Curl.Conformance.SshServer.UnitTests/SshServerConnectorTests.cs, Curl.Conformance.SshServer.UnitTests/SshServerRsaHostKeyTests.cs, Curl.Conformance.SshServer.UnitTests/SshServerUserAuthenticationTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4329 — Move SSH keys, host keys and user authentication into Curl.Ssh.UnitLibrary

## Goal

`Curl.Ssh.UnitLibrary` also holds SSH key formats, host key verification (known_hosts) and user authentication (RFC 4252), moved from the `Keys`, `HostKeys` and `Authentication` folders of `Curl.Protocol.Ssh.UnitLibrary`, which behaves exactly as before.

## Context

Second of three moves that give BL-4145's `--ssh-jump` extension an SSH client it may reference (ADR-0999). About 4,200 lines move with their tests (same folder names in `Curl.Protocol.Ssh.UnitTests`) into `Curl.Ssh.UnitTests`; namespaces become `Curl.Ssh.<Folder>`. Builds on BL-4328's project.

## Acceptance criteria

- [x] `Curl.Ssh.UnitLibrary` holds the keys, host keys and authentication code and `Curl.Ssh.UnitTests` their tests; `Curl.Protocol.Ssh.UnitLibrary` no longer holds those folders.
- [x] Every existing SSH unit and integration test still passes unchanged in behaviour.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (lane 1, second run): Finished the move the first run left uncommitted. `touches` widened to every file the move had to change outside the listed folders (the SSH integration tests, the conformance SSH server and its tests, `CurlCompositionSshVerboseTests.cs`, the root `SshInfoLines.cs` and `SshStateTrace.cs` the moved code logs through, `Curl.Ssh.UnitTests/Fakes` for the moved fakes); no task in Doing on `origin/work/dark-factory` named any of them. Each moved test file imports the `Curl.Ssh.<Folder>` namespaces it uses itself, as `Curl.Ssh.UnitTests` already did; that project has no global usings. `SshTransport.StartDelayedCompression` lost its covering calls when `SshUserAuthentication` left `Curl.Protocol.Ssh.UnitLibrary`, so `SshTransportTests.DelayedCompression.cs` now exchanges keys with `zlib@openssh.com` agreed both ways and checks the client's packets compress once it is called. Measured: `Measure-CodeQuality.ps1` shows Curl.Ssh.UnitLibrary and Curl.Protocol.Ssh.UnitLibrary at 100% line and branch with no failing member; the fast tests pass (113 assemblies, none failing) and the 6 SSH integration tests pass. CI on Linux and macOS: the change only moves code and adds a test with no path, file or OS dependence, so it stays platform-neutral.

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-29 (lane 5): Filed by BL-4145 (the --ssh-jump extension), which may not reference Curl.Protocol.Ssh.UnitLibrary (ADR-0999), so the SSH client it needs moves into a shared layer first.

## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 23ab75f6 5e26d2e1 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Keys, host keys and user authentication live in Curl.Ssh.UnitLibrary with their tests; both SSH libraries meet the gates
