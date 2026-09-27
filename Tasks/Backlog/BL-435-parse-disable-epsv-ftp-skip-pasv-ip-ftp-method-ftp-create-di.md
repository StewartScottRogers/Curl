---
id: BL-435
title: Parse --disable-epsv, --ftp-skip-pasv-ip, --ftp-method, --ftp-create-dirs, -l and -Q onto ITransferContext
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-435 — Parse --disable-epsv, --ftp-skip-pasv-ip, --ftp-method, --ftp-create-dirs, -l and -Q onto ITransferContext

## Goal

`--disable-epsv`, `--ftp-skip-pasv-ip` (and `--no-ftp-skip-pasv-ip`), `--ftp-method`, `--ftp-create-dirs`, `-l/--list-only` and `-Q/--quote` are parsed into `CommandLineOptions` with curl 8.21.0's refusals and carried on `ITransferContext`, so an FTP handler can read them; no FTP behaviour changes in this task.

## Context

- First half of the "FTP control options" work, split by the planner because the whole of it spans the option parser, the transfer-context contract, the console's context factory and the FTP handler. BL-436 implements the behaviour in `FtpProtocolHandler` and waits on this task. The handler itself came from BL-431, with its conversation in ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`), whose Consequences list these options as not implemented.
- Today these names are only in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` (`disable-epsv`, `ftp-skip-pasv-ip`, `ftp-method`, `ftp-create-dirs`, `list-only`/`l`, `quote`/`Q`) and the help table; `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has no entry for any of them, so they set nothing.
- Follow the TFTP precedent (ADR-0006, `Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md`): `CommandLineOptionTable` entries (`CommandLineOption.NegatableFlag` for `--disable-epsv`, `--ftp-skip-pasv-ip`, `--ftp-create-dirs`, `--list-only`; `CommandLineOption.Value` for `--ftp-method` and a repeatable `-Q`), properties on `CommandLineOptions`, matching properties on `ITransferContext` and `TransferContext` in `Curl.Protocol.Abstractions.UnitLibrary`, and the mapping in `Curl.Console/TransferContextFactory.cs` (where `TftpBlockSize` and `TftpNoOptions` are mapped today).
- `--ftp-skip-pasv-ip` defaults to on in curl 8.21.0 (ADR-0093 relies on it), so the context property must default to `true` and `--no-ftp-skip-pasv-ip` sets it `false`.
- Measure curl 8.21.0's refusal for a bad `--ftp-method` value (e.g. `--ftp-method bogus`) and a missing value for `--ftp-method` and `-Q` before pinning the messages and exit codes. Check the curl 8.21.0 manual (https://curl.se/docs/manpage.html) for the accepted `--ftp-method` values and the `-Q` command prefixes, and carry `-Q` commands as given, in order; interpreting their prefixes is BL-436's.
- Record the new context properties in an addendum to ADR-0006 or a new ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests` show each of `--disable-epsv`, `--ftp-skip-pasv-ip`/`--no-ftp-skip-pasv-ip`, `--ftp-method <value>` for every value curl 8.21.0 accepts, `--ftp-create-dirs`, `-l`/`--list-only` and repeated `-Q` setting its `CommandLineOptions` property, with the later of a pair of negatable flags winning.
- [ ] A bad `--ftp-method` value is refused with the `CurlExitCode` and message curl 8.21.0 printed, pinned in a named `Curl.Cli.UnitTests` test.
- [ ] `ITransferContext` and `TransferContext` expose the six settings with XML doc comments; the `--ftp-skip-pasv-ip` property defaults to `true`; a `Curl.Protocol.Abstractions.UnitTests` test pins the defaults.
- [ ] A `Curl.Console.UnitTests` test shows `TransferContextFactory` copying each setting from `CommandLineOptions` onto the context.
- [ ] The ADR addendum or new ADR describing the properties exists under `Documentation/Planning/Decisions/`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Cli.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
