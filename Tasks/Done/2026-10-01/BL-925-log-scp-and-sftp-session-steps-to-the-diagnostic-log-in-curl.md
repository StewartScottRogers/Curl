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
completed: 2026-10-01
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

- [x] `Curl.Protocol.Ssh.UnitTests` pin: the negotiated algorithms at `info`; the host key fingerprint and verdict at `info`; a refused password then accepted key (or keyboard-interactive) at `warning` then `info`; an SFTP `SSH_FX_NO_SUCH_FILE` at `error` naming its `CurlExitCode`; message numbers at `verbose`; and the no-secret tests above.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- Design (decided under ADR-0222; no new ADR, the wording is this library's): one `SshDiagnosticLog` wraps `ITransferContext.DiagnosticLog`. The handler builds it, passes the context's log on `ConnectTarget.DiagnosticLog`, and hands it to `SshTransport` (optional constructor argument, so every existing `new SshTransport(...)` in the tests is unchanged); the packet reader and writer, `SshUserAuthentication`, `SshSessionChannel` and `SftpSession` reach it through the transport.
- Levels: `error` the ending failure as `failed with <CurlExitCode> (<n>): <message>`, plus ` (libssh2 <code>)` when `SshTransferException.Libssh2Code` is set (new: set by `SessionEstablishmentFailed`); `warning` each method that did not authenticate (`password did not authenticate the user`) and a host key taken without a known_hosts file; `info` server identification, negotiated algorithms (MAC `implicit` for AEAD), key exchange method and ms, host key type and `SHA256:` fingerprint, verdict, server's method list, method that authenticated, transfer start and end; `verbose` each SSH message number sent/received, each method tried, channel open and subsystem/exec requests, each SFTP request (type, id) and answer (type, or status).
- The agent is logged as the method `publickey (ssh-agent)` to tell it from the key file.
- Secrets: messages carry message numbers only, never payloads; tests pin no password (`-u user:s3cret`), no `--pass` and no 16-character run of the private key file in any verbose line.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10. 1477 SSH tests pass, every existing one unmodified.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SCP and SFTP sessions write their steps to the diagnostic log, component ssh, at error, warning, info and verbose, with no secret
