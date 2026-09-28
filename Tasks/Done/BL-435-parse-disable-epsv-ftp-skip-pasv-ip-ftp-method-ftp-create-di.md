---
id: BL-435
title: Parse --disable-epsv, --ftp-skip-pasv-ip, --ftp-method, --ftp-create-dirs, -l and -Q onto ITransferContext
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] Tests in `Curl.Cli.UnitTests` show each of `--disable-epsv`, `--ftp-skip-pasv-ip`/`--no-ftp-skip-pasv-ip`, `--ftp-method <value>` for every value curl 8.21.0 accepts, `--ftp-create-dirs`, `-l`/`--list-only` and repeated `-Q` setting its `CommandLineOptions` property, with the later of a pair of negatable flags winning.
- [x] A bad `--ftp-method` value is refused with the `CurlExitCode` and message curl 8.21.0 printed, pinned in a named `Curl.Cli.UnitTests` test.
- [x] `ITransferContext` and `TransferContext` expose the six settings with XML doc comments; the `--ftp-skip-pasv-ip` property defaults to `true`; a `Curl.Protocol.Abstractions.UnitTests` test pins the defaults.
- [x] A `Curl.Console.UnitTests` test shows `TransferContextFactory` copying each setting from `CommandLineOptions` onto the context.
- [x] The ADR addendum or new ADR describing the properties exists under `Documentation/Planning/Decisions/`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Cli.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Console`.

## Notes

- Plan: follow the TFTP precedent (ADR-0006). Six `CommandLineOptionTable` rows (four `NegatableFlag`, `--ftp-method` and a repeatable `-Q` as `Value` accepting empty), six `CommandLineOptions` properties, a new `FtpFileMethod` enum and six `ITransferContext`/`TransferContext` members in `Curl.Protocol.Abstractions`, mapped in `TransferContextFactory`.
- Measured curl 8.21.0 (Windows, 2026-09-27): a bad `--ftp-method` value (`bogus`, `single`, `''`) is NOT refused. curl prints `Warning: unrecognized ftp file method '<value>', using default` (wrapped at 79 columns, dropped under `-s`), uses `multicwd` and carries on; valid values are ASCII case-insensitive. The second acceptance criterion's "refusal" is therefore pinned as that warning in `CommandLineFtpOptionTests.Parse_FtpMethodCurlDoesNotRecognise_WarnsAndUsesMultiCwd` (and its wrap and `-s` siblings). A missing value for `--ftp-method`/`-Q`/`--quote` exits 2 with `requires parameter`, pinned too. `-Q ''` is accepted.
- Decision (ADR-0006 addendum, decided by Claude under Stewart's delegation): `--ftp-method` is carried as the enum `FtpFileMethod` rather than a string; `-Q` values are carried verbatim, prefixes included, for BL-436 to interpret.
- `touches` widened to the ADR-0006 file, which the acceptance criteria require; no task in Doing names it.
- Coverage: `Curl.Cli.UnitLibrary` and `Curl.Protocol.Abstractions.UnitLibrary` report 100/100 with no failing member. `Curl.Console` reports one failing member, `DiskWriteOutFileOpener.TryOpen`, which this task does not touch; it is pre-existing and already filed as BL-432. Every member this task changed in `Curl.Console` is covered.
- Tests: Curl.Cli.UnitTests 2011 passed; Curl.Protocol.Abstractions.UnitTests 512 passed; Curl.Console.UnitTests 861 passed; the whole fast run green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The six FTP control options parse onto CommandLineOptions and reach ITransferContext; a bad --ftp-method warns and uses multicwd as curl 8.21.0 does
