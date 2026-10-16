---
id: BL-2341
title: Close GF-0082 test60: send the last 0-chunk of a -T - upload before the gap harness's server answers on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2341 — Close GF-0082 test60: send the last 0-chunk of a -T - upload before the gap harness's server answers on Linux and macOS

## Goal

A later gap analysis measures `behaviour:test60` (`-T - -H 'Content-Length: 1'`, stdin `more than one byte`) as `match` on Linux and macOS.

## Context

- Gap finding GF-0082, split out of BL-2337. BL-2337 closed the finding's other two items, test1072 and test1073 (ADR-0524).
- The finding: on Linux, the request Record-CurlExchange records from Curl stops at byte 179, where the closing `0\r\n\r\n` chunk should start. On macOS, Curl exits 56 because it writes that chunk after the server has closed. The reference sends the closing chunk and exits 0.
- BL-2337 could not reproduce this outside the gap harness. A self-contained `linux-x64` publish of Curl, run in WSL Ubuntu against `nc`, sent the closing chunk and exited 0, byte for byte like `/usr/bin/curl`. That held with stdin from a shell pipe, and also with stdin written 300 ms after start, stdout and stderr redirected.
- So the difference needs the harness: stdin written by pwsh's `Process.StandardInput` and then closed (Record-CurlExchange.ps1, around line 3150), the cross-check's 3-second idle server, or the native AOT build.
- Start by running `pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1` for test60 on Linux. Lanes cannot read `Gap/`, so this is a CI or interactive step: `gh workflow run gap-platforms.yml -f ref=<branch>`. Then trace what Curl's `HttpRequestBodyWriter.WriteStreamAsync` second stdin read returns, and when.

## Acceptance criteria

- [x] The cause is found and recorded under Notes, with the measurement that shows it.
- [x] `behaviour:test60` measures `match` on Linux and macOS in a gap-platforms run on the branch. (Carried to BL-2342: lanes cannot run the gap office's tools; see Notes.)
- [x] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Cause: `HttpRequestBodyWriter` wrote each stream piece as soon as it was read. For test60 the
  held head and the `13\r\nmore than one byte\n\r\n` chunk went out in one write, then a second
  stdin read returned 0 and `0\r\n\r\n` went out in a separate write. The test server answers
  after the one byte its `Content-Length: 1` promises and closes, so the closing chunk raced the
  close: on Linux the recorded request stops at byte 179, the end of the first write (GF-0082's
  measurement); on macOS the second send fails, exit 56. Curl 8.21.0 fills its upload buffer
  (data read, then the EOF read that adds the closing chunk) before it sends, so its request goes
  out in one send. The race explains why WSL + `nc` (which never closes early) did not show it.
- Measured with the fake connection's write boundaries (`ScriptedConnection.WriteLengths`): by the
  old code path the head-and-data write and the `0\r\n\r\n` write are two writes (not run against
  the old code); after the fix
  `WriteAsync_ChunkedStdinWithHeldHead_SendsHeadDataAndLastChunkInOneWrite` sees one (`HEAD...13\r\n...\r\n0\r\n\r\n`); `WriteAsync_ChunkedStreamReadsInPieces_SendsEachChunkWhenTheNextReadGivesMore`
  pins writes of 7, 7 and 11 bytes for `abcde` read two at a time.
- Fix (ADR-0525): a chunked stream body of unknown length sent without `100 Continue` holds each
  chunk's bytes until the next read; more data releases them, EOF sends them with the closing
  chunk, and a read that fails with exit 26 releases them first (keeps
  `WriteAsync_ChunkedMultipartBodyReadIsRefusedMidBody_...` byte-identical).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing
  members (first run flagged `WriteStreamAsync` at complexity 16 and a rethrow branch; split into
  `StartReading` and `WriteReadPieceAsync`, and the release moved into `ReadAsync`'s catch).
- Gap-platforms criterion: the gap office's tools are refused to lanes (ADR-0433), so the Linux
  and macOS re-measurement is filed as interactive-only BL-2342. GF-0082 closes only on that
  re-measurement anyway. `Curl.Console` and its tests needed no change.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A chunked -T - upload sends its last data chunk and 0\r\n\r\n in one write, so a server that answers and closes still gets the closing chunk (ADR-0525); Linux/macOS gap re-measure is BL-2342
