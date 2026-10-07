---
id: BL-463
title: Parse --ftp-pasv (clears -P) and --epsv into CommandLineOptions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-457]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-463 — Parse --ftp-pasv (clears -P) and --epsv into CommandLineOptions

## Goal

`--ftp-pasv` and `--epsv` parse as curl 8.21.0 parses them: `--ftp-pasv` clears an earlier `-P`/`--ftp-port` (so `CommandLineOptions.FtpPort` is `null` again) and `--epsv`/`--no-epsv` are the inverse of `--disable-epsv`.

## Context

- Both names are only in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` today; `CommandLineOptionTable.cs` has no row, so a script using them is not yet a drop-in.
- Measured by BL-457 with `Record-CurlExchange.ps1 -Ftp` (curl 8.21.0, Windows, 2026-09-27): `-P - --ftp-pasv` sends `EPSV`, not `EPRT`; `--no-ftp-pasv` exits 2 with `curl: option --no-ftp-pasv: the given option cannot be reversed with a --no- prefix`.
- Measure `--ftp-pasv -P -` (order) and `--epsv`/`--no-epsv` against `--disable-epsv` before pinning.
- BL-457 added `FtpPort` and `FtpUseEprt`; `--disable-eprt`/`--eprt` in `CommandLineOptionTable` show the inverse-flag pattern.

## Acceptance criteria

- [x] Named tests in `Curl.Cli.UnitTests` pin `-P - --ftp-pasv` (FtpPort `null`), the measured order behaviour, `--no-ftp-pasv` refused as not reversible, and `--epsv`/`--no-epsv` with `--disable-epsv` (later wins).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

Filed by BL-457.

Measured with `Record-CurlExchange.ps1 -Ftp` (curl 8.21.0, Windows, 2026-09-27): `-P - --ftp-pasv` sends `EPSV`; `--ftp-pasv -P -` sends `EPRT` (the later wins); `--no-ftp-pasv` exits 2 as not reversible; `--ftp-pasv=x` and `--epsv=x` are accepted with the value ignored; `--disable-epsv --epsv` sends `EPSV`, `--epsv --disable-epsv` and `--disable-epsv --no-epsv` and `--no-epsv` send `PASV`, `--no-epsv --no-disable-epsv` sends `EPSV` (the later wins).

Delivered as two rows in `CommandLineOptionTable`: `ftp-pasv` is a plain `Flag` that sets `FtpPort` to `null` (so its `--no-` spelling is refused by the existing path), and `epsv` is a `NegatableFlag` writing `FtpDisableEpsv = !on`, the pattern `--eprt` uses. No design choice was left open once measured, so no ADR. Tests: `CommandLineFtpActiveModeAndSslOptionTests`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --ftp-pasv clears -P and --epsv/--no-epsv invert --disable-epsv, later wins, as curl 8.21.0 does
