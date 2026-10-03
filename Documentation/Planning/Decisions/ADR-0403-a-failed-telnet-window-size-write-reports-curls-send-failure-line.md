# ADR-0403 — A failed telnet window size write reports curl's `Send failure: <text>` line

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

curl 8.21.0's `sendsuboption` (`lib/telnet.c`, lines 671-716) sends a NAWS subnegotiation as three
writes. The header (`IAC SB NAWS`) and footer (`IAC SE`) are `swrite`s whose failure is
`failf(data, "Sending data failed (%d)", SOCKERRNO)`. The 4-byte window size between them goes
through `send_telnet_data`, which polls the socket for writing and calls `Curl_xfer_send`; telnet
ignores the result. `Curl_xfer_send` reaches the socket filter's `cf_socket_send`, which for any
send error other than "would block" writes `failf(data, "Send failure: %s", curlx_strerror(sockerr,
...))`. BL-1307 left that write silent on failure because the line was not measured.

BL-1312 tried to measure it on Windows (curl 8.21.0, Schannel) with `Record-CurlExchange.ps1` and
`-sv -t WS=80x24 telnet://127.0.0.1:PORT`, in six set-ups: the server answering `DO NAWS` and
closing; closing with a TCP RST (the new `-ResetAfterResponse` switch); `DO TERM-TYPE` first so a
first write meets a closed peer (BL-1307's set-up, which produced its failures on 2026-10-02);
2 KB, 20 KB and 200 KB of filler before `DO NAWS`; and `--limit-rate`, which telnet ignores. In every
run curl's writes all reached the socket before the peer's close took effect, so no write failed and
curl printed no failure line at all (task Notes). The failure is a race loopback does not lose.

## Decision

Follow curl's source: when the window size write fails with a socket error, Curl writes
`Send failure: <text>` between the header's and footer's `Sending data failed (N)` lines, and the
session goes on. `<text>` is what `curlx_strerror` gives on each platform: on Windows the Schannel
build's own Winsock table, `Connection was reset` and `Connection was aborted` (the words its
`Recv failure:` lines were measured with, ADR-0088), and the error's own message for anything else;
elsewhere the error's own message, which .NET takes from the C library's `strerror`, as curl does.
`TelnetSocketErrorText` holds the choice and `TelnetOutbox.SendTelnetData` queues the write.

## Consequences

The line is pinned from source, not from a run: `TelnetProtocolHandlerSendFailureTests` pins
`Send failure: Connection was aborted` on Windows and `Send failure: Software caused connection
abort` elsewhere. A measurement that someday catches the race and shows different text overrides
this ADR. The error buffer is untouched, as before: the session still exits 0.

## Alternatives considered

- **Stay silent (BL-1307's choice).** Every path through `cf_socket_send` that fails a send writes
  the line; silence is the one output curl's code cannot produce under `-v`.
- **Block the task until the race is measured.** No set-up within the recorder's reach loses the
  race, and the source leaves only one reading.
