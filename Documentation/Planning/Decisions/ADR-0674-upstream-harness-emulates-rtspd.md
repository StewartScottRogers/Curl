# ADR-0674 — The upstream harness emulates rtspd on %RTSPPORT

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation (BL-2617)
- Supersedes decision 2 of ADR-0457

## Context

ADR-0457 built no `rtspd` stand-in because every case naming `%RTSPPORT` was a `<tool>` libtest
and the harness ran none. Since BL-2556 and BL-2558 the runner runs ported libtest programs, and
BL-2532 and BL-2551 ported lib567 to lib572 and lib3100, so cases 567 to 572, 577, 689 and 3100 now
reach the screening step and skip only for "the harness does not emulate the rtsp server".
ADR-0457's decision 3 said the stand-in would be built then.

## Decision

1. `RtspServerConnector` in `Curl.Conformance.UnitLibrary` emulates upstream's
   `tests/server/rtspd.c` at `curl-8_22_0` on port 8996 (`%RTSPPORT`), just inside the Gopher
   stand-in, and screening lets `rtsp` cases run.
2. As rtspd does, each `RtspServerConnection` request ends at its empty line plus its first
   `Content-Length` header's bytes, picks `<dataN>` from the number after the path's last slash
   (N its last four digits when over 10000), is answered with that part as written (`crlf` and
   `nonewline` applied, so an `RTSP/` status line keeps its bare line feed, as upstream's
   `subnewlines` keeps it), and is followed by the part's interleaved RTP packets from the
   `<servercmd>` `rtp: part P channel C size S [size_err E]` lines (`RtspInterleavedPackets`:
   `$`, channel byte, 16-bit length S + E, S bytes repeating `$_1234\n\0Rsdf`). The connection
   stays open until a reply holds `swsclose` or is empty.
3. rtspd neither echoes `CSeq` nor tracks sessions: the cases' replies carry those themselves,
   so the stand-in sends nothing it does not read from the part. Its Digest, NTLM and Basic
   part-number rules, `auth_required`, `idle`, `stream`, `pipe`, `skip`, its 404 document and
   `<postcmd>` are not emulated, since no case at `curl-8_22_0` naming the `rtsp` server uses them.
4. Each completed request is recorded in its `ReceivedBytes`, which the runner merges into
   `<verify><protocol>` with the other servers' dumps, as rtspd appends to `server.input`.

## Consequences

- The 9 RTSP cases are measured. On 2026-10-17 all of them differ in the client, not the server:
  the `Libcurl` engine sends every RTSP transfer as the tool's `OPTIONS *` and never carries out
  `CURLOPT_RTSP_REQUEST`, `CURLOPT_RTSP_STREAM_URI`, `CURLOPT_RTSP_TRANSPORT` or the interleave
  callback. That work is filed as its own task, which also holds the cases on the ratchet.
