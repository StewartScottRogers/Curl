---
id: BL-1283
title: Write curl's Received ACK, Received last DATA and Received unexpected DATA -v lines for TFTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1283 — Write curl's Received ACK, Received last DATA and Received unexpected DATA -v lines for TFTP

## Goal

Under `-v`, a `tftp://` transfer that receives an out-of-order packet writes the info line curl 8.21.0 writes for it: `Received ACK for block N, expecting M` on an upload, and `Received last DATA packet block N again.` or `Received unexpected DATA packet block N, expecting block M` on a download.

## Context

- curl 8.21.0 `lib/tftp.c` (tag `curl-8_21_0`, https://github.com/curl/curl/blob/curl-8_21_0/lib/tftp.c):
  - `tftp_tx`, lines 371-393: an ACK whose block is not `state->block` (the block last sent) writes `infof(data, "Received ACK for block %d, expecting %d", rblock, state->block)`, counts a retry and re-sends the last DATA packet (or fails with `tftp_tx: giving up waiting for block %d ack`, exit 55, once the retries are used up).
  - `tftp_rx`, lines 532-549: a DATA block equal to the last block received writes `infof(data, "Received last DATA packet block %d again.", rblock)` (with the full stop) and acknowledges it again; any other block that is not the next one writes `infof(data, "Received unexpected DATA packet block %d, expecting block %d", rblock, NEXT_BLOCKNUM(state->block))` and is otherwise ignored.
  - Each `infof` is one `* ` line under `-v` and nothing under `-s` without `-v`.
- Curl today, `Curl.Protocol.Tftp.UnitLibrary`:
  - `TftpUpload.AcceptAcknowledgementAsync` (`TftpUpload.cs`, the `block != lastSentBlock` branch) re-sends the last packet with no line.
  - `TftpDownload.AcceptDataAsync` (`TftpDownload.cs`) acknowledges a repeat of the last block again (`block == expectedBlock - 1`) and returns `null` for any other unexpected block, both with no line.
  - The `-v` info lines of a TFTP transfer are written through `TftpTransferEvents` (`TftpTransferEvents.cs`, e.g. `TimedOut` writes `Timeout waiting for block N ACK. Retries = R`); the new lines belong there beside it.
- Block numbers print as unsigned 16-bit values (`getrpacketblock` returns the packet's two bytes), so after a wrap the expected block of a download is 0 where the last block was 65535.
- Comparison: the curl 8.21.0 source above. `Record-CurlExchange.ps1 -Tftp -TftpReply 'ACK1=DROP'` on an upload (`-T`) makes curl re-send DATA 1, which the recorder answers by re-sending its last packet; if that provokes the `Received ACK` line, pin the measured stderr bytes in the test as well, and say in the test comment which it is.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Tftp.UnitTests` drives an upload through the fake datagram channel whose server answers DATA 1 with ACK 0 and asserts the info line `Received ACK for block 0, expecting 1`, followed by the re-sent DATA 1, then completes the upload normally.
- [ ] A test drives a download whose server sends DATA 1 twice and asserts `Received last DATA packet block 1 again.` once, that the block's bytes reach the output once, and that ACK 1 is sent twice.
- [ ] A test drives a download whose server sends DATA 3 after DATA 1 and asserts `Received unexpected DATA packet block 3, expecting block 2`, that nothing is acknowledged for it, and that the transfer completes when DATA 2 arrives.
- [ ] A test pins the wrapped case: a download whose last block received was 65535 and that then receives block 1 writes `Received unexpected DATA packet block 1, expecting block 0`.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- Out of scope, a known gap for a later task: `tftp_tx` also accepts ACK 65535 when it expects block 0 (the tftpd-hpa wrap bug, lines 377-383); `TftpUpload.AcceptAcknowledgementAsync` does not.

## Log

- 2026-10-02: Created.
