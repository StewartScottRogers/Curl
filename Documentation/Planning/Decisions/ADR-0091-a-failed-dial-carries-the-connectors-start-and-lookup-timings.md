# ADR-0091 — A failed dial carries the connector's start and lookup timings

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0075 made a failed connect end the transfer's timings, but `ConnectResult.Failed` carried
no `ConnectTimings`, so `%{time_namelookup}` after a refused connect printed `0.000000`.
Measured with curl 8.21.0 (mingw, Schannel) on 2026-09-27 (BL-287, BL-382):

| Command | Exit | Output |
| --- | --- | --- |
| `curl -s -o NUL -w "ns=%{time_namelookup}\|c=%{time_connect}" http://127.0.0.1:1/` | 7 | `ns=0.000048\|c=0.000000` |
| the same for `http://nonexistent.invalid/` | 6 | `ns=0.000000\|c=0.000000` |

`ConnectTimings.Connected` was a non-nullable `long`, so a failed connect had no way to leave
`%{time_connect}` unset: a `0` timestamp would print as the smallest non-zero time, not `0`.

## Decision

- `ConnectTimings.Connected` becomes `long?`; `null` means the connect never completed, and
  `%{time_connect}` then prints `0.000000`, as for every other unset timestamp.
- `ConnectResult` gains `Failed(CurlExitCode, string, ConnectTimings?)` and
  `Refused(string, ConnectTimings?)`; the existing overloads pass `null`.
- `TcpConnector` passes `Started` and `NameResolved` with `Connected` `null` when a dial reaches
  no address, to the host or to a proxy. A failed resolve still carries none (exit 6 measured
  `0`). A proxy's refused dial was not measured separately; it is the same dial failure, and
  curl's lookup time covers the proxy's resolve on a success too.
- `HttpProtocolHandler`'s failed-connect report copies `ConnectResult.Timings` into
  `TransferTimings.Connect`.

## Consequences

`%{time_namelookup}` after a refused HTTP connect prints the lookup time and
`%{time_connect}` stays `0`, as curl does. The Dict, Gopher, MQTT and Telnet handlers still
report no timings on a failed connect; they did not before either. Readers of
`ConnectTimings.Connected` must allow for `null`; `TransferWriteOutVariables` already reads it
through `?.`.

## Alternatives considered

- Keep `Connected` non-nullable and store `Started` in it: `%{time_connect}` would print
  `0.000001`, not curl's `0.000000`.
- A separate `FailedConnectTimings` type: a second name for the same timestamps, for one
  missing value a nullable already expresses.
