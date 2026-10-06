---
id: BL-576
title: Register the SSH handler for scp and sftp in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-562, BL-569, BL-574, BL-952]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0248-the-console-finds-the-ssh-known-hosts-file-as-curls-tool-does.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-576 — Register the SSH handler for scp and sftp in Curl.Console

## Goal

`curl sftp://...` and `curl scp://...` run end to end through `Curl.Console` with the SSH handler and the TCP connector, the SSH options (BL-562) and `-u`, `--key`, `--pass`, `-Q`, `-T`, `-r`, `-C` mapped into the context, where today they fail as an unsupported protocol.

## Context

- Conformance audit 2026-09-28, row 35. Handler: BL-563 to BL-574; options: BL-561, BL-562.
- Register in `Curl.Console/CurlComposition.cs`; map options in `TransferContextFactory.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `scp`/`sftp` with default port 22; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run an `sftp://` download and an `scp://` download through a fake connector backed by the in-memory SSH peer from `Curl.Protocol.Ssh.UnitTests` (or an equivalent test double), pinning stdout, stderr and exit code as measured by BL-569 and BL-574.
- [x] Each SSH option reaches the context unchanged, with tests.
- [x] `curl -V` lists `scp` and `sftp`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- 2026-09-29 (lane 4): there is no SSH handler to register yet. `Curl.Protocol.Ssh.UnitLibrary`
  has the transport, host key check, authentication, `SftpFileDownload` and `ScpFileDownload`,
  but no `IProtocolHandler` composing them, and every other protocol keeps its handler in its
  own library. Writing it in `Curl.Console` would put protocol logic in the executable, so it
  is filed as BL-952 (touches the SSH library and its tests, which BL-568 in `Doing` holds)
  and this task now depends on it. BL-952 also exposes the in-memory SSH peer this task's
  `Curl.Console.UnitTests` need. No code was changed by this run.
- From BL-568 (ADR-0230): `publickey` runs only when `SshUserAuthentication` is given an
  `SshUserKeySource` built from the transfer's `SshOptions`, the file system,
  `Environment.GetEnvironmentVariable` and ADR-0022's credential encoding; without one it
  is skipped.
- 2026-09-29 (lane 5): delivered. `CurlComposition.CreateProtocolHandlers` registers
  `SshProtocolHandler` over the recording connector, `PhysicalFileSystem`,
  `WindowsReference`/`OpenSslReference` by platform and ADR-0022's encoding. `SshOptionsMapping`
  fills `ITransferContext.Ssh` for `scp`/`sftp` only; `-u`, `-Q`, `-T`, `-r`, `-C` already reach
  their own members. The runner resolves the known-hosts file (`SshKnownHostsFileSearch`,
  ADR-0248) after proxy and credentials. `Curl.Core`'s `ProtocolDispatcher` needed no change:
  `CurlUrlScheme` already gives both schemes port 22.
- Touches widened: `Curl.Cli.UnitLibrary`/`Curl.Cli.UnitTests` (the `-V` Protocols line lives in
  `CurlVersionText`) and the new ADR-0248 file; no task in `Doing` names them.
- Measured with curl 8.21.0 (Git for Windows mingw64, libssh2 1.11.1) on 2026-09-29, env vars
  pointed at temp dirs, `sftp://127.0.0.1:1/x`: no known_hosts -> `curl: Could not find a
  known_hosts file` + `curl: (2) Failed initialization`, exit 2, nothing under `-s`;
  `--hostpubmd5`/`--hostpubsha256` -> `Warning: Could not find a known_hosts file`, then connects;
  `-k` or `--knownhosts` -> connects; found in `CURL_HOME`, `HOME`, `USERPROFILE`, `APPDATA`
  but not `XDG_CONFIG_HOME`; `sftp... http...` stops after the sftp failure, `http... sftp...`
  runs the http first. Real `-V` also lists `ipfs ipns` and `libssh2/1.11.1`; left as ADR-0021
  decides (only what Curl serves, TLS backend alone named).
- `Curl.Console.UnitTests` now references `Curl.Protocol.Ssh.UnitTests` for `InMemorySshServer`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waiting on BL-952: no SshProtocolHandler exists in Curl.Protocol.Ssh.UnitLibrary to register
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. curl sftp:// and scp:// download through SshProtocolHandler in Curl.Console, SSH options mapped, known_hosts resolved as curl does, -V lists scp and sftp
