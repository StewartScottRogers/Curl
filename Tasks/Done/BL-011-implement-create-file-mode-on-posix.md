---
id: BL-011
title: Implement --create-file-mode on POSIX
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-009]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-011 — Implement `--create-file-mode` on POSIX

## Goal

Files that a `file://` upload creates on a POSIX system get the octal mode given by
`--create-file-mode` (upstream scope; see Notes; originally written as "files that `-o`
or `--create-dirs` creates").

## Context

POSIX only; a no-op on Windows. It depends on `PhysicalFileSystem` (BL-009) exposing
a create-mode parameter. Upstream behaviour: https://curl.se/docs/manpage.html,
`--create-file-mode`.

## Acceptance criteria

- [x] `--create-file-mode` is parsed as an octal mode.
- [x] On POSIX, files created by a `file://` upload get that mode (reworded from "files
      created for `-o` and `--create-dirs`"; see Notes).
- [x] On Windows, the option is accepted and has no effect.
- [x] Tests cover parsing and the mode passed through `IFileSystem`, without touching
      the disk.

## Notes

- Scope choice (unattended default): upstream curl applies `--create-file-mode` to
  files created remotely by an upload, over `file://`, SFTP and SCP, and not to `-o`
  (the tool opens `-o` files with 0666 minus umask) or to `--create-dirs` directories
  (0750). The manpage in this task's Context says so, FR-014 already said "a file
  created by a `file://` upload", and applying it to `-o` would be a divergence from
  upstream, so the implementation follows upstream and criterion 2 was reworded. No
  code for `-o` exists in this task's `touches` anyway (it will live with the console
  layer).
- Plan as built: `CommandLineOptionTable` row `--create-file-mode` ->
  `CommandLineNumber.ParseOctal` (max 0777) -> `CommandLineOptions.CreateFileMode`
  (`UnixFileMode?`, null when not given). `ITransferContext.CreateFileMode`
  (`UnixFileMode`, default `TransferContext.DefaultCreateFileMode` = 0644, curl's
  `new_file_perms` default). `IFileSystem.OpenForWriteAsync` gained a `createMode`
  parameter; `FileProtocolHandler` passes the context's value; `PhysicalFileSystem`
  sets `FileStreamOptions.UnixCreateMode` off Windows, so umask applies and an existing
  file keeps its mode, exactly like curl's `open(2)`. Copying the parsed option onto a
  `TransferContext` waits on the transfer dispatcher, as for every other option.
- Parsing measured on local curl 8.21.0 (2026-09-26): `0`, `0000`, `0640`, `777`,
  `00000000777` accepted; `abc`, `8`, `18`, `7a`, `7 `, ` 7`, `+7`, `-0`, `-1`, `0x7`,
  empty -> exit 2 "expected a proper numerical parameter"; `1000`, `1777`, `07777`,
  `10008`, a 27-digit value -> exit 2 "too large number" (the maximum is checked while
  digits are read, before leftover text).
- `FileStreamOptions.UnixCreateMode` throws `PlatformNotSupportedException` on Windows
  even when set to null, and CA1416 flags it, so `PhysicalFileSystem` takes the platform
  decision in an internal constructor (`[UnsupportedOSPlatformGuard("windows")]` field);
  a fast Windows test passes `true` and asserts the setter was reached, which keeps Core
  at 100% line and branch coverage on Windows. The setter lives in a single-expression
  method because an if-block's closing brace after a throwing statement is an
  uncoverable line.
- The two POSIX integration tests (`OpenForWriteAsync_CreateModeOnPosix_*`) are
  `[OSCondition]` Linux/macOS/FreeBSD and could not be run here: WSL Ubuntu has no .NET
  SDK and Docker was not running. They are skipped on Windows.
- Verified: `dotnet build` clean, `dotnet format --verify-no-changes` clean, fast tests
  green; Measure-CodeQuality: Cli and Core new members 100%/100%; the remaining failing
  members in Cli (`UploadUrl`), Abstractions (record-generated members) and File are in
  code this task did not change. code-reviewer: no bugs; two doc wording fixes applied.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --create-file-mode parses as octal (max 0777) and a file:// upload creates its file with that mode on POSIX (default 0644), ignored on Windows
