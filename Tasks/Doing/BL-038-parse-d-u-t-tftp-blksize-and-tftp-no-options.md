---
id: BL-038
title: Parse -d, -u, -t, --tftp-blksize and --tftp-no-options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-037, BL-032]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-038 — Parse -d, -u, -t, --tftp-blksize and --tftp-no-options

## Goal

The option parser accepts `-d`/`--data`, `-u`/`--user`, `-t`/`--telnet-option`,
`--tftp-blksize` and `--tftp-no-options`, recording them on `CommandLineOptions` under the
names ADR-0006 gives their `ITransferContext` members.

## Context

Builds on the parser BL-037 creates in `Curl.Cli.UnitLibrary`. The option meanings are
in ADR-0006
(`Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md`)
and <https://curl.se/docs/manpage.html>, checked against curl 8.21.0. Measured with the
local curl 8.21.0 on 2026-09-26:

- `curl -t` and `curl --data` with no value are exit 2, `requires parameter`.
- `--tftp-blksize abc` is exit 2, `expected a proper numerical parameter`; values out of
  the 8-65464 range (5, 70000) are accepted by the parser and clamped later by the TFTP
  handler, so the parser records them unchanged.
- `-u bob:se:cret` reaches an MQTT server as user `bob`, password `se:cret`: the split is
  at the first colon.
- `-t BOGUS=1` and `-t TTYPE` get past the parser and fail at transfer time with exit 48
  and exit 49, so the parser records `-t` values verbatim and validates nothing.

Model members: `PostData` (the `-d` bytes, UTF-8 encoded from the argument), `Credentials`
(`-u user:password` split at the first colon), `TelnetOptions` (every `-t` value in
order), `TftpBlockSize` and `TftpNoOptions`.

Scope limits, each to be filed as its own task during the run rather than guessed: the
`-d @file` and `-d @-` forms, several `-d` joined with `&`, and `-u user` with no colon
(upstream prompts for a password).

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests` show `-d 75` and `--data 75` give `PostData` bytes
      `37 35`; `-u bob:secret` gives user `bob`, password `secret`; `-u bob:se:cret`
      gives password `se:cret`.
- [ ] A test shows `-t TTYPE=vt100 -t XDISPLOC=host:0` gives `TelnetOptions`
      `["TTYPE=vt100", "XDISPLOC=host:0"]` in that order, and `-t BOGUS=1` and `-t TTYPE`
      are recorded, not refused.
- [ ] Tests show `--tftp-blksize 1024` gives 1024, `--tftp-blksize 5` gives 5, and
      `--tftp-blksize abc` is refused with exit 2 and the two measured lines;
      `--tftp-no-options` sets `TftpNoOptions`.
- [ ] Tests show `-t`, `--data`, `-u` and `--tftp-blksize` as the last argument are
      refused with the measured `requires parameter` line naming the option as spelled.
- [ ] The out-of-scope forms in `Context` are filed as tasks and their IDs recorded in
      `Notes`.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
