---
id: BL-1212
title: Fail a TFTP option acknowledgement curl rejects with its exit 71 messages
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-033
created: 2026-10-02
completed:
---
# BL-1212 — Fail a TFTP option acknowledgement curl rejects with its exit 71 messages

## Goal

A `tftp://` download or upload whose server answers with an OACK that curl 8.21.0's `tftp_parse_option_ack` rejects ends with exit 71 (`CurlExitCode.TftpIllegal`) and curl's message, sending no ACK, instead of quietly falling back to a 512-byte block size.

## Context

- Today `Curl.Protocol.Tftp.UnitLibrary/TftpPackets.cs` `ReadAcknowledgedBlockSize` returns `DefaultBlockSize` (512) for any `blksize` it cannot use, and `TftpDownload.AcceptOptionAcknowledgementAsync` (`TftpDownload.cs`) and the OACK case of `TftpUpload` (`TftpUpload.cs`) always answer with ACK 0. `Curl.Protocol.Tftp.UnitTests/TftpProtocolHandlerTests.cs` `ExecuteAsync_OptionAcknowledgement_DecidesBlockSize` pins that fallback with the rows "unparsable blksize keeps 512", "blksize below 8 keeps 512" and "blksize above 65464 keeps 512"; those rows date from the first TFTP commit (2026-09-26) and were never measured against an OACK, because `Record-CurlExchange.ps1 -Tftp` cannot send a chosen OACK (its `-TftpReply` has no OACK override).
- curl 8.21.0, `lib/tftp.c` at tag `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/tftp.c), `tftp_parse_option_ack` (lines 259-330), called for both directions from `tftp_receive_packet` (lines 1105-1110), which returns its error at once: no ACK and no ERROR packet are sent. For each option, after the `got option=(<name>) value=(<value>)` info line the transfer already reports:
  - an option or value without its terminating NUL (`tftp_option_get` returns NULL): `Malformed ACK packet, rejecting`;
  - `blksize` (name matched by exact length, ignoring case), read with `curlx_str_number(&value, &blksize, 65464)` (leading decimal digits, trailing text ignored): no leading digit or a value above 65464 is `blksize is larger than max supported (65464)`; 0 is `invalid blocksize value in OACK packet`; below 8 is `blksize is smaller than min supported (8)`; above the block size the request asked for (`--tftp-blksize` clamped, 512 by default, also under `--tftp-no-options`) is `server requested blksize larger than allocated (<value>)`;
  - `tsize` on a download only, when its digits parse to 0: `invalid tsize -:<rest>:- value in OACK packet`, where `<rest>` is the value after the digits `curlx_str_number` consumed (empty for `0`). An unparsable `tsize`, and any `tsize` on an upload, is ignored.
  All of these are exit 71 (`CURLE_TFTP_ILLEGAL`, https://curl.se/libcurl/c/libcurl-errors.html).
- Requirement FR-033 in `Documentation/Product/Requirements.md` covers OACK handling; this task does not edit it (the doc stays out of `touches`).

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Tftp.UnitTests` drive a download through the fake datagram channel with each OACK above (`blksize\0abc\0`, `blksize\065465\0`, `blksize\00\0`, `blksize\07\0`, `blksize\01024\0` with no `--tftp-blksize`, `tsize\00\0`, and an option with no value terminator) and assert exit 71, curl's exact message, and that nothing is sent after the request.
- [ ] A test pins that a `blksize` of `1024x` with `--tftp-blksize 1024` is accepted as 1024, and that an unparsable `tsize` on a download and any `tsize` on an upload are ignored.
- [ ] The same rejection applies to the OACK answering a write request: a test drives an upload whose OACK carries `blksize\07\0` and asserts exit 71 `blksize is smaller than min supported (8)` with no DATA 1 sent.
- [ ] The three fallback rows of `ExecuteAsync_OptionAcknowledgement_DecidesBlockSize`, and any other test that relied on the fallback or on accepting a `blksize` larger than the one requested, are replaced by the rejections; the test comments cite `lib/tftp.c` lines 259-330 at `curl-8_21_0` and say the OACK cases are pinned from the source because the recorder cannot send one.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- No new measurement: `Record-CurlExchange.ps1 -Tftp` answers a request with its own OACK and cannot be told to send another. Extending it touches the root script, which is outside this task's `touches`; leave that to a separate task.

## Log

- 2026-10-02: Created.
