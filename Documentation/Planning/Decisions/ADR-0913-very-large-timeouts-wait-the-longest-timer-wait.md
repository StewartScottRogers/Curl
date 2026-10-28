# ADR-0913: A timeout past what .NET can arm waits as long as .NET can

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3568, CGR-039)

## Context

curl reads `-m`, `--connect-timeout`, `--keepalive-time` and `--speed-time` into a C `long`. On Windows
that is 32 bits, and Curl already refuses the same values curl's Schannel build does
(`expected a proper numerical parameter`, exit 2). On Linux and macOS it is 64 bits, so curl takes
`-m 4294968`, `--connect-timeout 4294968`, `--keepalive-time 922337203686` and
`--speed-time 922337203686` and runs the transfer. Curl failed each before connecting with .NET
exception text and exit 2: a .NET timer, `Task.Delay` or `CancellationTokenSource` waits at most
4294967294 milliseconds (about 49.7 days), and a `TimeSpan` holds at most 922337203685 whole seconds.

Measured on 2026-10-26 in WSL (curl 8.18.0 OpenSSL) against `file:///etc/hostname`, a closed port and a
one-shot `nc` server: real curl exits 0, 7 and 0 for each value; Curl, built for linux-x64, now does the
same with the same stdout and request bytes (BL-3568 Notes).

## Decision

1. `MaxTimeWatchdog` arms its timer for at most `MaxTimeWatchdog.LongestTimerWait` (4294967294 ms) and,
   as it already did when a timer fires early, sets it again for the time left, again at most that long.
   It keeps the exact `-m` for its messages and log lines, so any caller (libcurl's `CURLOPT_TIMEOUT_MS`
   included) can give it any positive limit.
2. `EasyOptionsFromCommandLine` holds `-m` and `--connect-timeout` to that longest wait, because the
   handlers and connectors arm their own deadlines from them (the HTTP handler's `HttpTransferDeadline`,
   the TCP, UDP-tunnel and SSH connect limits), and `--keepalive-time` and `--speed-time` to the most
   whole seconds a `TimeSpan` holds. A wait that long never ends in a real run, so no output changes.
3. The one visible difference left is `--libcurl` text for such a value on Linux and macOS, which writes
   the held value rather than the one typed; no repro or test reaches it, and matching it would mean
   carrying the typed number beside the `TimeSpan` in every option record.

## Consequences

- The four repro commands of CGR-039 match curl on Linux; Windows, which refuses them while parsing, is
  unchanged.
- A libcurl caller setting `CURLOPT_CONNECTTIMEOUT_MS` or `CURLOPT_TIMEOUT_MS` past 49.7 days on Linux
  still reaches the handlers' own deadlines unheld; that path is not the command line's and is left to
  its own task if one is filed.
