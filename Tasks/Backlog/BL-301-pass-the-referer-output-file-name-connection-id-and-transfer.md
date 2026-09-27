---
id: BL-301
title: Pass the referer, output file name, connection id and transfer id to TransferWriteOutVariables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-235]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-301 — Pass the referer, output file name, connection id and transfer id to TransferWriteOutVariables

## Goal

`TransferWriteOutVariables` takes the referer, the output file name, the connection id and the transfer id from `Curl.Console`, and prints `%{referer}`, `%{filename_effective}`, `%{conn_id}` and `%{xfer_id}` as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0041 records why BL-284 left these unknown.
- Measured: `-e http://ref.example/x` printed `http://ref.example/x`, no `-e` printed nothing; `-o out.bin` printed `out.bin`, `-O` printed `wo.txt`, stdout printed nothing; `conn_id` and `xfer_id` were `0` for the first transfer and `1` for the second of two URLs to a server that closes each connection; a URL curl rejected (exit 3) printed `conn_id` `-1`.
- These are known to the command line and the process, not to a handler (ADR-0015, "What the report does not carry"), so they are constructor inputs. `conn_id` and `xfer_id` count per process.
- BL-235 wires `TransferWriteOutVariables` into `Curl.Console`; this builds on it.

## Acceptance criteria

- [ ] Each of the four variables renders as measured above, pinned in `TransferWriteOutVariablesTests` and in a `Curl.Console.UnitTests` test for the two-URL case.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Output` and `Curl.Console`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
