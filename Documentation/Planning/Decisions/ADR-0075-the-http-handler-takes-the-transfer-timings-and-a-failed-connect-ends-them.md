# ADR-0075 — The HTTP handler takes the transfer timings, and a failed connect ends them all

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-226 formats the `-w` times from `TransferReport.Timings` (ADR-0035), and `TcpConnector`
takes the connect's timestamps (ADR-0030). BL-287 found that no handler set
`TransferReport.Timings`, so every `%{time_*}` printed `0.000000`, and that a failed connect
returned no report at all. Measured against curl 8.21.0 (mingw, Schannel, the Windows
reference, ADR-0018) on 2026-09-27, `-w "ns=%{time_namelookup}|c=%{time_connect}|pre=%{time_pretransfer}|post=%{time_posttransfer}|st=%{time_starttransfer}|t=%{time_total}"`:

| Case | curl 8.21.0 |
| --- | --- |
| `http://127.0.0.1:1/`, refused | exit 7, `ns=0.000048\|c=0.000000\|pre=2.030259\|post=2.030259\|st=2.030259\|t=2.030262` |
| `http://nonexistent.invalid/` | exit 6, `ns=0.000000\|c=0.000000\|pre=0.055769\|post=0.055769\|st=0.055770\|t=0.055774` |
| loopback server that reads the request and closes | exit 52, `ns=0.000047\|c=0.000807\|pre=0.000908\|post=0.000908\|st=0.000000\|t=0.301441` |
| loopback server that answers the head after 0.3 s and the body 0.3 s later | exit 0, `ns=0.000062\|c=0.000869\|pre=0.000956\|post=0.000957\|st=0.301662\|t=0.602455` |

So a literal address has a lookup time, `time_starttransfer` is the first response byte and
stays `0` when none arrives, and after a failed connect curl takes pretransfer, posttransfer
and starttransfer as the transfer ends.

## Decision

1. `HttpProtocolHandler` reports `TransferTimings` on every report it builds: `Started` when
   `ExecuteAsync` begins, shared by every retry of the transfer; `Connect` from the connect
   the exchange ran on; `RequestReady` just before the first request byte is written;
   `RequestSent` once the body is sent; `FirstByteReceived` from
   `HttpFirstByteTimingConnection`, which the response is read through and which takes the
   moment the first read returns bytes; and `Completed` when the report is built.
2. A failed connect, of any exit code, returns a report whose `RequestReady`, `RequestSent`,
   `FirstByteReceived` and `Completed` are one timestamp taken as it failed, with no
   `Connect`, and `UsedProxy` as before.
3. `TcpConnector` keeps recording `NameResolved` for a literal address (ADR-0030); BL-287
   pins it with a test.

## Consequences

- `%{time_*}` prints real values for HTTP and HTTPS transfers.
- A connect failure now has a report where it had none; every reader already treats a
  missing report as an empty one, so only the timings change.
- `%{time_namelookup}` after a refused connect is still `0.000000` where curl prints the lookup
  time: `ConnectResult.Failed` carries no timings, and changing that is a change to
  `Curl.Protocol.Abstractions`, filed as a follow-up. The same follow-up aligns
  `ConnectTimings.NameResolved`'s doc comment, which still says it is `null` for a literal.
- Other handlers (FTP, file and the rest) still report no timings.

## Alternatives considered

- Taking `FirstByteReceived` when the head reader returns: simpler, but that is the end of the
  head, not its first byte, and would be off by the head's arrival time on a slow server.
- Leaving a failed connect's timings unset: prints `0.000000` where curl prints the total.
