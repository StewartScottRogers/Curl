---
id: BL-491
title: Parse -N/--no-buffer and flush each write to standard output while it is set
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-491 — Parse -N/--no-buffer and flush each write to standard output while it is set

## Goal

`-N`, `--no-buffer` and `--buffer` are accepted, and while buffering is off every block of body bytes the handler writes reaches the output stream at once (flushed per write) instead of waiting in a buffer, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 3 (Blocker): Curl refuses `-N` with exit 2.
- The alias-table row is `buffer` with letter `N` and a documented `--no-` form (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`): `-N` means `--no-buffer`, and `--buffer` turns buffering back on.
- The output streams are opened in `Curl.Console` (`StandardOutputOpener.cs`, `OutputFileTarget.cs`, `DeferredOutputFileStream.cs`); find where standard output is buffered and add a flush-per-write wrapper only when buffering is off.
- `-N` is a common streaming idiom (`curl -N https://.../events`), which is why the audit ranks it a Blocker.

## Acceptance criteria

- [x] `-N`, `--no-buffer` and `--buffer` parse (last one wins) into a named `CommandLineOptions` property; `Curl.Cli.UnitTests` covers each spelling and the bundle `-sN`.
- [x] A `Curl.Console.UnitTests` test with a fake handler that writes two blocks and a recording output stream shows each block flushed before the next is written with `-N`, and not flushed per write without it.
- [x] Standard output bytes are unchanged by `-N` (same bytes, same order) for a loopback-style fake transfer.
- [x] New tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and `-Library Curl.Console` report 100% line and branch coverage and no failing member.

## Notes

- Measured with the local curl 8.21.0 (Schannel) on 2026-09-28: `-N`, `--no-buffer`, `--no-buffer=x`, `--buffer` and `-sN` are accepted; `--no-no-buffer` exits 2 as unknown.
- Parser: `CommandLineOptions.NoBuffer`, set by a new row kind `CommandLineOption.NegatableFlagTurnedOffByShortName("buffer", 'N', ...)`. Its `ShortNameTurnsOff` makes a bundle letter apply `Negate` instead of `Apply`, mirroring curl's `nobuffer = longopt ? !toggle : TRUE`. Chosen over a second row or a check on the spelled option, because the parser stays table-driven and a `-K` line spelled either way follows the same path.
- Console: `FlushEachWriteStream` wraps the body output under `-N` (applied beneath `--limit-rate`). Default taken: it also wraps an `-o` file's output, not only standard output, because curl's `tool_write_cb` flushes whatever `outs` the body goes to while `nobuffer` is set. Standard output itself was already opened unbuffered; the wrapper makes each write's flush explicit, which is what matters under the failure-deferring stream and for a console stream.
- Tests: `CommandLineNoBufferTests` (Cli), `FlushEachWriteStreamTests` and `CurlCommandRunnerNoBufferTests` (Console) with the new `WriteAndFlushRecordingStream` fake. Coverage: Curl.Cli.UnitLibrary 100/100 (572 members, 0 failing), Curl.Console 100/100 (418 members, 0 failing).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -N/--no-buffer/--buffer parse, and under -N every body write is flushed before the next
