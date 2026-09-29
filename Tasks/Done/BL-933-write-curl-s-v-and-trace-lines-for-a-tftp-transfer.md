---
id: BL-933
title: Write curl's -v and --trace lines for a TFTP transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-932]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-933 — Write curl's -v and --trace lines for a TFTP transfer

## Goal

A `tftp://` download or `-T` upload writes curl 8.21.0's `-v` lines and its `--trace`/`--trace-ascii` blocks byte for byte, where today the TFTP handler reports no transfer event at all.

## Context

- Audit 2026-09-29, part B: no file in `Curl.Protocol.Tftp.UnitLibrary` calls any `ITransferEvents` member, so `curl -v tftp://…` prints none of curl's TFTP lines and `--trace` has no blocks. The connect lines for dict, gopher, telnet and mqtt were done by an archived task; TFTP was not part of it.
- Where: `TftpProtocolHandler.cs`, `TftpDownload.cs` (and the upload path), `TftpRetrySchedule.cs` (retransmission timing curl reports), `TftpErrorMapping.cs` (the error-packet text curl prints). Take the sink from `ITransferContext.Events`.
- Measure first with BL-932's `Record-CurlExchange.ps1 -Tftp` (a dependency): `-v`, `--trace-ascii -` and `--trace -` for a download, a download with `--tftp-blksize 1024`, a download with `--tftp-no-options`, a `-T` upload, an ERROR 1 reply (exit 68) and a dropped ACK (a retransmission). Copy every line into Notes with curl's version and build before pinning any text; pin only what was measured, and which of curl's lines are info lines, which data blocks.
- Not in this task: the diagnostic log (BL-927).

## Acceptance criteria

- [x] Measured output for the six cases is copied into Notes.
- [x] `Curl.Protocol.Tftp.UnitTests` pin, through a recording `ITransferEvents`, every measured `-v` info line and every data block's bytes in curl's order, for the six cases.
- [x] Existing TFTP tests pass unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-29)

curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, Release-Date 2026-06-24 (Git for
Windows' mingw64 build), through `Record-CurlExchange.ps1 -Tftp`. `--trace-ascii` shown;
`-v` prints the same `* ` lines (CRLF-ended on stderr) and `{ [6 bytes data]` for the data
block; `--trace` prints the block as `0000: 68 65 6c 6c 6f 0a    hello.`. Lines starting
`*` are info lines; `<= Recv data` is a data-in block. `Total` varied 299998-300000 with
the milliseconds already elapsed.

1. Download (`tftp://127.0.0.1:47601/file.txt`, served `hello\n`):
   ```
   *   Trying 127.0.0.1:47601...
   * Established connection to 127.0.0.1 (127.0.0.1 port 47601) from  port 0 
   * set timeouts for state 0; Total 299999, retry 6 maxtry 50
   * got option=(tsize) value=(6)
   * tsize parsed from OACK (6)
   * got option=(blksize) value=(512)
   * blksize parsed from OACK (512) requested (512)
   * got option=(timeout) value=(6)
   * Connected for receive
   * set timeouts for state 1; Total 0, retry 5 maxtry 3
   <= Recv data, 6 bytes (0x6)
   0000: hello.
   * shutting down connection #0
   ```
2. `--tftp-blksize 1024`: as 1, with `got option=(blksize) value=(1024)` and
   `blksize parsed from OACK (1024) requested (1024)`.
3. `--tftp-no-options` (no OACK; the server answers with DATA 1):
   ```
   *   Trying 127.0.0.1:47603...
   * Established connection to 127.0.0.1 (127.0.0.1 port 47603) from  port 0 
   * set timeouts for state 0; Total 299998, retry 6 maxtry 50
   <= Recv data, 6 bytes (0x6)
   0000: hello.
   * Connected for receive
   * set timeouts for state 1; Total 0, retry 5 maxtry 3
   * shutting down connection #0
   ```
4. `-T up.txt` (`upload me`, 9 bytes) - no data-out block at all:
   ```
   *   Trying 127.0.0.1:47604...
   * Established connection to 127.0.0.1 (127.0.0.1 port 47604) from  port 0 
   * set timeouts for state 0; Total 299999, retry 6 maxtry 50
   * got option=(tsize) value=(9)
   * got option=(blksize) value=(512)
   * blksize parsed from OACK (512) requested (512)
   * got option=(timeout) value=(6)
   * Connected for transmit
   * set timeouts for state 2; Total 0, retry 5 maxtry 3
   * shutting down connection #0
   ```
5. `RRQ=ERROR 1 File not found`, exit 68, stderr `curl: (68) TFTP: File Not Found`:
   ```
   *   Trying 127.0.0.1:47605...
   * Established connection to 127.0.0.1 (127.0.0.1 port 47605) from  port 0 
   * set timeouts for state 0; Total 299999, retry 6 maxtry 50
   * TFTP error: File not found
   * shutting down connection #0
   ```
6. Retransmission. `ACK1=DROP` on a one-block download shows nothing (curl is done once
   it has sent ACK 1), so the dropped packet measured is `DATA1=DROP` (curl re-sends ACK 0
   after ~5 s) and, for an upload, `ACK1=DROP` (curl re-sends DATA 1):
   ```
   ... as 1 through "set timeouts for state 1; Total 0, retry 5 maxtry 3"
   * Timeout waiting for block 1 ACK. Retries = 1
   <= Recv data, 2 bytes (0x2)
   0000: a.
   * shutting down connection #0
   ```
   ```
   ... as 4 through "set timeouts for state 2; Total 0, retry 5 maxtry 3"
   * Timeout waiting for block 2 ACK. Retries = 1
   * shutting down connection #0
   ```

### Decisions (defaults taken)

- New `TftpTransferEvents` holds the wording, beside `TftpTransferLog`; the handler reports
  the connect lines once the channel opens and `shutting down connection #0` after it is
  disposed. The `Established` line is a plain info line, not `ReportConnectionOpened`,
  because curl leaves the local end empty (`from  port 0 `) for its unconnected UDP socket.
- Connection number: always `#0`. The datagram seam carries no number; filed BL-968 for
  several URLs in one invocation.
- `Total` is `TftpRetrySchedule.TimeLeftMilliseconds`: the whole milliseconds left the
  schedule was derived from, 0 when no limit applies (curl's `timeout_ms`). Tests freeze
  the clock, so state 0 pins `Total 300000`.
- `Timeout waiting for block N ACK. Retries = R` is reported only once the server has
  answered (curl's RX/TX states); N is the next block (download: the block expected;
  upload: the block after the last sent), R the retry count including this one. An
  unanswered RRQ/WRQ re-send and a too-short-packet re-send report nothing: neither was
  measured (the recorder cannot drop a request), and curl's START state prints no line.
- `blksize parsed` follows `TftpPackets.ReadAcknowledgedBlockSize`'s rule (name `blksize`,
  8-65464); `tsize parsed` is download-only, as measured. The requested size is
  `TftpPackets.RequestedBlockSize`, or 512 under `--tftp-no-options`, curl's default.
- `TFTP error: <text>` only for text ending in a NUL, as curl's `tftp_strnlen` check.
- An empty last block reports no data-in block.
- End-to-end run of the built `curl.exe` against the recorder was not possible in this lane
  (`bin/` is outside the session's permitted paths); the unit tests pin the events.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. tftp:// downloads and -T uploads report curl 8.21.0's -v lines and --trace data blocks
