---
id: BL-925
title: Log SCP and SFTP session steps to the diagnostic log in Curl.Protocol.Ssh
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-925 — Log SCP and SFTP session steps to the diagnostic log in Curl.Protocol.Ssh

## Goal

`Curl.Protocol.Ssh.UnitLibrary` writes the diagnostic log (component `ssh`) from `ITransferContext.DiagnosticLog` for each SCP and SFTP session step: identification exchange, algorithm negotiation, key exchange, host key check, user authentication, channel and SFTP requests.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Transport/SshIdentificationExchange.cs` (server identification string), `Negotiation/` (algorithms offered and chosen per direction), `KeyExchange/` (method run, elapsed ms), `HostKeys/` (key type, fingerprint, known_hosts result), `PacketProtection/` (cipher and MAC in force, rekey), `Authentication/SshUserAuthentication.cs` (methods the server allows, method tried, result), `Connection/` (channel open, exec or subsystem), `Sftp/` (each SFTP request type and status). Take the log from the transfer context the handler receives; pass it on its `ConnectTarget`.
- What, per level: `error` the failure that ends the session with its `CurlExitCode` and the libssh2 code (`Libssh2ErrorCode`) it maps from; `warning` an authentication method refused before another succeeded, a host key accepted by `--insecure`; `info` server identification, the negotiated algorithms, host key fingerprint and verdict, the authenticated method, transfer start and end with bytes and ms; `verbose` each SSH message number sent and received and each SFTP request and status.
- Credential-bearing paths: a password (`-u user:s3cret`), a key pass phrase (`--pass`) and a private key file: no recorded message contains the password, the pass phrase or any private key byte.
- BL-574 (SCP download, in Doing), BL-568 to BL-573, BL-576 to BL-578 and others touch this library too; the board serialises them. This task instruments whatever exists when it runs, and the ADR's rule makes each later SSH task keep logging its own steps.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` pin: the negotiated algorithms at `info`; the host key fingerprint and verdict at `info`; a refused password then accepted key (or keyboard-interactive) at `warning` then `info`; an SFTP `SSH_FX_NO_SUCH_FILE` at `error` naming its `CurlExitCode`; message numbers at `verbose`; and the no-secret tests above.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
