---
id: BL-305
title: Pass the referer, output file name, connection id and transfer id to TransferWriteOutVariables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-235]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-305 — Pass the referer, output file name, connection id and transfer id to TransferWriteOutVariables

## Goal

`TransferWriteOutVariables` takes the referer, the output file name, the connection id and the transfer id from `Curl.Console`, and prints `%{referer}`, `%{filename_effective}`, `%{conn_id}` and `%{xfer_id}` as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- Measured: `-e http://ref.example/x` printed `http://ref.example/x`, no `-e` printed nothing; `-o out.bin` printed `out.bin`, `-O` printed `wo.txt`, stdout printed nothing; `conn_id` and `xfer_id` were `0` for the first transfer and `1` for the second of two URLs to a server that closes each connection; a URL curl rejected (exit 3) printed `conn_id` `-1`.
- These are known to the command line and the process, not to a handler (ADR-0015, "What the report does not carry"), so they are constructor inputs. `conn_id` and `xfer_id` count per process.
- BL-235 wires `TransferWriteOutVariables` into `Curl.Console`; this builds on it.

## Acceptance criteria

- [x] Each of the four variables renders as measured above, pinned in `TransferWriteOutVariablesTests` and in a `Curl.Console.UnitTests` test for the two-URL case.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Output` and `Curl.Console`.

## Notes

- Touches widened to `Documentation/Planning/Decisions` for ADR-0060 (Decided by Claude under Stewart's delegation). No task in Doing names that folder.
- Plan and review done in-session rather than by subagents: four dictionary entries, four init properties and three lines of wiring.
- Design (ADR-0060): init properties `Referer`, `OutputFileName`, `ConnectionId` (default -1), `TransferId` on `TransferWriteOutVariables`, so the constructor and its ~30 test callers are unchanged. `Curl.Console` sets `Referer` from `-e`, `OutputFileName` from the output stream's final path (the `-J` name if chosen, `--output-dir` included), `TransferId` from the URL index, and `ConnectionId` from a per-run counter over transfers that do not end with exit 1 or 3 (exit 3 measured as -1; exit 1 assumed to match, rejected at the same stage).
- Not modelled: under `-e ";auto" -L` curl prints the last referer sent; filed as BL-361.
- Default taken: no fresh curl measurement; BL-284's Notes (2026-09-26, curl 8.21.0 Schannel) already hold every case the criteria name.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Output 251, Curl.Console 599 passed); `Measure-CodeQuality.ps1` Curl.Output.UnitLibrary 100/100, 0 failing, worst CRAP 10; Curl.Console 100/100 with `-IncludeIntegration` (without it, only `DiskWriteOutFileOpener.TryOpen` is uncovered, whose tests are Integration by design since BL-280), worst CRAP 10. Extracted `TakeConnectionId` to keep `TransferAllAsync` at complexity 10 or under.

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -w prints %{referer}, %{filename_effective}, %{conn_id} and %{xfer_id} as curl 8.21.0; follow-up BL-361
