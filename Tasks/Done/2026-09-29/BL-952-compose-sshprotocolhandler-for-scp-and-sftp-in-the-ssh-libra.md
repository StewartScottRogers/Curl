---
id: BL-952
title: Compose SshProtocolHandler for scp and sftp in the SSH library
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-568]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-952 — Compose SshProtocolHandler for scp and sftp in the SSH library

## Goal

`Curl.Protocol.Ssh.UnitLibrary` has a public `SshProtocolHandler : IProtocolHandler` claiming `scp` and `sftp` (default port 22) that connects through the injected `IConnector`, runs `SshTransport`, checks the host key with `SshHostKeyChecker` from `ITransferContext.Ssh`, authenticates with `SshUserAuthentication` (and BL-568's public key), and runs `SftpFileDownload` or `ScpFileDownload` into the context's output, mapping every `SshTransferException` to its `CurlExitCode` and message.

## Context

- Found by BL-576 on 2026-09-29: every other protocol's `IProtocolHandler` lives in its own library (`TftpProtocolHandler`, `SmbProtocolHandler`, ...), but BL-563 to BL-574 built the SSH pieces without the handler that composes them, so `Curl.Console` has nothing to register. BL-576 registers this handler once it exists.
- Pieces: `Transport/SshTransport.cs`, `HostKeys/SshHostKeyChecker.cs`, `Authentication/SshUserAuthentication.cs`, `Sftp/SftpFileDownload.cs`, `Scp/ScpFileDownload.cs`, `SshConnectionFailure.cs`; options come from `Curl.Protocol.Abstractions.UnitLibrary/SshOptions.cs` (BL-562) and the context's credentials (`-u`, `--key`, `--pass`).
- Failure messages and exit codes are already measured: ADR-0122, ADR-0206, ADR-0212, ADR-0215, ADR-0220, ADR-0225. The handler ends the session after a header failure (ADR-0225).
- `-Q`, `-T`, `-r`, `-C` and directory listing are BL-570 to BL-573 and stay out of this task.

## Acceptance criteria

- [x] `SshProtocolHandler` exists in `Curl.Protocol.Ssh.UnitLibrary`, claims exactly `scp` and `sftp`, and never constructs a `Socket`.
- [x] `Curl.Protocol.Ssh.UnitTests` run an `sftp://` and an `scp://` download end to end through the handler against the in-memory SSH peer in `Fakes`, pinning the bytes written and the outcome of BL-569's and BL-574's measured success and failure cases (missing file exit 78, channel open failure exit 79, host key mismatch, authentication failure exit 67).
- [x] The in-memory peer and a fake connector backed by it can be driven from another test project (public in `Curl.Protocol.Ssh.UnitTests`, or documented as copyable), so BL-576 can run `Curl.Console` against them.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through a separate architect stage: ADR-0122 already
  fixes the handler's role, seams and test peer (`InMemorySshServer` over an in-memory
  duplex `IConnection`), and ADR-0213/0215/0220/0225/0230 fix every step's order and
  message, so no new ADR was needed.
- **Order**, from those ADRs: narrow host keys from the known-hosts file (before
  connecting, so an unknown entry type is exit 79 with nothing connected), connect,
  `NegotiateAlgorithmsAsync`, `ExchangeKeysAsync`, `RequestServiceAsync`,
  `SshHostKeyChecker.Check`, `AuthenticateAsync` (with an `SshUserKeySource` for
  `publickey`), then `SftpFileDownload` or `ScpFileDownload`.
- **Constructor** (default taken): public `(IConnector, IFileSystem, SshAlgorithmPreferences,
  Encoding)` - the console passes the platform preset (ADR-0122) and
  `CredentialEncoding.ForPlatform` (ADR-0022), so this library does not reference
  `Curl.Authentication`. Random source, ephemeral keys and the `HOME` reader default to the
  system ones behind an internal constructor.
- **Teardown** (default taken): `DISCONNECT` 11 `Shutdown` (ADR-0215) is sent after every
  outcome once the first key exchange has finished, and not after a handshake failure,
  since there is no session to end; a write that fails because the server has gone is
  ignored. Cancellation skips it. `SftpFileDownload` now closes its channel (`EOF`, then
  `CLOSE` after the server's) after closing the handle, as ADR-0220 measured and left to
  the handler; the scripted SFTP tests are unaffected.
- **Test peer**: `Fakes.InMemorySshServer` (public, `IConnector`) runs a real server-side
  session per connection (`InMemorySshServerSession`) over `Fakes.InMemoryDuplexConnection`:
  `diffie-hellman-group14-sha256` with an `rsa-sha2-256` host key (in both the Windows and
  the OpenSSL preset, so both are tested on every platform), `aes128-ctr` +
  `hmac-sha2-256`, `password` and `publickey` authentication, the `sftp` subsystem and
  `scp -pf`. Its public members use only BCL and `Curl.Protocol.Abstractions` types, so
  BL-576 can reference `Curl.Protocol.Ssh.UnitTests` and hand it to `Curl.Console` as the
  connector; `Events` records what each session saw, `HostKeySha256` and
  `KnownHostsLine(host)` give what `--hostpubsha256` and a known-hosts file need.
- The measured failure cases pinned through the handler: SFTP missing file exit 78, SCP
  missing file exit 78 `Failed to recv file`, SCP channel refused exit 79 `Channel open
  failure (connect failed)`, SFTP channel refused exit 2, host key mismatch exit 60 (both
  `--hostpubsha256` and known hosts), wrong password exit 67 `Authentication failure`,
  no common key exchange exit 2 `-5`, unknown known-hosts entry type exit 79.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SshProtocolHandler serves scp and sftp end to end against the in-memory SSH server, 100% covered
