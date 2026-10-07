---
id: BL-141
title: Match curl when a TFTP download receives a datagram under four bytes
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-127]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-141 — Match curl when a TFTP download receives a datagram under four bytes

## Goal

A TFTP download that receives a datagram under four bytes does what curl 8.21.0 does, instead of ignoring it.

## Context

- Found by BL-127. curl 8.21.0's `tftp_receive_packet` turns a datagram under four
  bytes into `TFTP_EVENT_TIMEOUT` after `failf("Received too short packet")`. Measured
  for an upload (BL-127 Notes): the last packet is re-sent at once and a retry counted,
  and the transfer's eventual failure message is `Received too short packet`.
  `TftpUpload` now does this.
- `TftpDownload.AnswerAsync` still ignores such a datagram. The download case has not
  been measured: measure it first (short datagram in reply to the RRQ, and after DATA 1)
  with curl 8.21.0 against a loopback Python UDP server.

## Acceptance criteria

- [x] `Notes` records what curl 8.21.0 sends and when, and the exit code and message,
      for a short datagram in reply to the RRQ and after DATA 1.
- [x] A test on the fake clock pins each measured case in `TftpDownloadRetransmissionTests`.
- [x] `dotnet build` is clean, the fast tests are green, and
      `Curl.Protocol.Tftp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

### Measured with curl 8.21.0 (mingw64, Schannel), 2026-09-26, lane 1

Loopback Python UDP server that takes the RRQ on its listening port and answers from a
second (transfer) port; `curl -o out tftp://127.0.0.1:<port>/x`. The short datagram is
`00 03` (2 bytes), sent from the transfer port.

- **Short reply to the RRQ, then silence, `--connect-timeout 10`** (RRQ `timeout 3`):
  the RRQ is re-sent at once to the *listening* port, then at 4.05 s (the schedule from
  t = 0, unmoved); exit **7** `curl: (7) Received too short packet` at 8.2 s.
- **Three short replies to the RRQ:** the RRQ is re-sent twice at once, the third ends
  the transfer with exit **7** `Received too short packet` at t = 0.
- **Short reply to the RRQ, `-m 5`** (RRQ `timeout 1`): RRQ at 0, at once, 2.03 s; the
  retries run out before `MaxTime`, exit **7** `Received too short packet` at 4.1 s.
- **Short reply to the RRQ, then DATA 1 (10 bytes):** RRQ re-sent at once, then ACK 1;
  exit 0, 10 bytes written. The short datagram pinned the transfer port.
- **DATA 1 (512 bytes), then short, then silence (no options):** ACK 1, ACK 1 at once on
  the short one, then on the usual 6 s schedule from the first ACK (short sent 3.0 s
  and 3.5 s after DATA 1: re-sends at 6.05 / 6.54 s, not 9 s, so the schedule does not
  move); exit **28** `Received too short packet` after 3 re-sends.
- **DATA 1, then four short datagrams:** ACK 1 re-sent three times at once, the fourth
  ends it with exit **28** `Received too short packet` at t = 0.
- **DATA 1, short, then silence, `-m 5`:** ACK 1 at 0, at once, then about 2 s apart;
  exit **28** `Received too short packet` at 5.09 s (the `MaxTime` message replaced).
- **DATA 1, short, then DATA 2 (final):** ACKs 1, 1, 2; exit 0, 522 bytes.
- **DATA 1, short, then ERROR 1:** exit **68** `Received too short packet`.
- **Short reply to the RRQ, then ERROR 1:** exit **68** `Received too short packet`.
- **DATA 1, short, then DATA 2 from a third port:** exit **56**
  `Received too short packet`, nothing sent to the stranger.

So the download follows the same rule as the upload (BL-127): a datagram under four
bytes is curl's `TFTP_EVENT_TIMEOUT` - the last packet re-sent at once, a retry counted,
the next scheduled re-send left where it was - and `Received too short packet` is the
message of whatever failure later ends the transfer.

### Delivered 2026-09-26 (lane 1)

- `TftpDownload` now does this: `AnswerTooShortAsync` notes the datagram and re-sends
  through the new `ResendAsync` (which does not touch `resendAt`), ending with
  `RetriesRunOut` (7 before an answer, 28 after) when the retries are spent;
  `RunAsync` puts `Received too short packet` on any failure after one. Silence re-sends
  through the same `ResendAsync`, then sets `resendAt`.
- To keep `AnswerAsync` at complexity 10 (it measured 11 with the new branch), it is now a
  plain dispatcher returning the `ValueTask`, with `IsFromStranger` (endpoint pinning)
  and `AcceptOptionAcknowledgementAsync` extracted.
- Tests: 11 new in `TftpDownloadRetransmissionTests`;
  `ExecuteAsync_ShortDatagramUnexpectedOpcodeAndWrongBlock_AreIgnored` in
  `TftpProtocolHandlerTests` became `ExecuteAsync_UnexpectedOpcodeAndWrongBlock_AreIgnored`,
  since a short datagram is no longer ignored. Tftp tests 76 -> 87 (includes BL-140's);
  library 100% line and branch, max complexity 10.
- Choices taken (sensible defaults):
  - The fake channel hands scripted datagrams out at t = 0, so the "schedule does not
    move" rule is pinned by the measured times of a short datagram arriving at t = 0
    (re-sends at 0, then 6, 12) rather than one arriving mid-interval; the code path is
    the same `ResendAsync`, which never touches `resendAt`.
  - The short datagram pins the server endpoint before its length is checked, as curl's
    `tftp_receive_packet` does and as measured (a short reply followed by DATA from the
    same port completes).
  - `curl` re-checks timeouts on whole wall-clock seconds, so measured re-sends drift by
    up to a second; the tests pin the whole seconds curl's rules give, as BL-075 and
    BL-127 did.
- Review (code-reviewer): no findings.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A TFTP download answers a datagram under four bytes as curl 8.21.0 does: last packet re-sent at once, a retry counted, and the failure that ends it says Received too short packet
