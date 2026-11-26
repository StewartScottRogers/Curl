# ADR-1428: What -Z reports for the transfers behind a telnet session that fails fatally

- Status: Accepted
- Date: 2026-11-26
- Task: BL-6035

Decided by Claude under Stewart's delegation.

## Context

Under `-Z` a `telnet` session holds back every later transfer until it ends (ADR-1420). When the
session itself ends with a fatal code (exit 43, a subnegotiation asking for a refused `-t` value),
Curl reported every later transfer as aborted (exit 42) without performing it.

Measured on curl 8.22.0 (Schannel, Windows) on 2026-11-26: a loopback telnet server sending
`IAC DO NEW-ENVIRON`, `IAC DO TTYPE` and the `SB ... SEND` requests, with `-t NEW_ENV=nocomma`,
followed by HTTP transfers, standard input empty. Standard output and standard error were captured
separately; the exit code is 43 in every row. `t` is the telnet transfer.

| `--parallel-max` | Behind `t`    | Standard output (`exitcode urlnum http_code size_download`) | Standard error |
| ---------------- | ------------- | ----------------------------------------------------------- | -------------- |
| 1                | 1 HTTP        | `43 0 000 0`, `43 1 000 0`                                  | 43, 43         |
| 1                | 2 HTTP        | `43 0 000 0`, `43 1 000 0`                                  | 43, 43         |
| default          | 1 HTTP        | `43 0 000 0`, `hello`, `42 1 200 6`                         | 43, 42         |
| 2                | 2 HTTP        | `43 0 000 0`, `hello`, `42 1 200 6`, `43 2 000 0`           | 43, 42, 43     |
| default          | 2 HTTP        | `43 0 000 0`, `42 1 000 0`, `42 2 000 0`                    | 43, 42, 42     |
| default          | 1 HTTP, 3 s   | `43 0 000 0`, `42 1 000 0`                                  | 43, 42         |
| default          | before `t`, 3 s | `43 1 000 0`, `42 0 000 0`                                | 43, 42         |

The one-HTTP and two-HTTP default rows gave the same output in each of four repeats.

## Decision

For the transfers held in the run's first batch behind such a session:

1. One lone transfer within `--parallel-max` is started. A response that beats the one-second abort
   delay is written and the transfer is reported as exit 42 with its response code and byte count;
   one that does not is cancelled and reported as exit 42. Its own failure, such as exit 7, stands.
2. Two or more transfers within `--parallel-max` are reported as exit 42 without starting.
3. A transfer past `--parallel-max` is reported with the session's code and libcurl's text for it.
4. No transfer past the first batch is started or reported.

A transfer that was already running when the session failed is reported by rule 1.

## Why

Each rule is the output measured. Why curl gets a lone waiting transfer to its response and not two
follows from how its multi loop interleaves them; Curl pins what is observed instead of modelling the
loop.

## Consequences

- Rule 1's "own failure stands" was not measured; it keeps a connection error visible.
- A `telnet` session that starts after the first batch is unchanged for the transfers not yet started.
