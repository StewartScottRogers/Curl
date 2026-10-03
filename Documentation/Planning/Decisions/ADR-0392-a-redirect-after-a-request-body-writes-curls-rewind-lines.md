# ADR-0392: A redirect after a request body writes curl's rewind lines

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1213
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0, given `-d ab -L` and answered with a `302` or `307`, writes `Need to rewind upload for next
request` under plain `-v` right after the status line. Under `--trace-config read` it also writes
`[READ] client reader needs rewind before next request` before that line,
`[READ] client_reset, will rewind reader` in place of the hop's `clear readers`, and
`[READ] client start, rewind readers` before `Issue another request to this URL`. It writes none of
them without `-L`, after a `404`, or for `-d ''` (measured 2026-10-02, BL-1213 Notes). ADR-0383 put the
hop's reset line in `Curl.Console`'s `ClientReaderResetTraceEvents`.

## Decision

1. `HttpProtocolHandler` writes `Need to rewind upload for next request` after a `3xx` status line when
   `-L` is given and the request has a body whose length is not 0, through a new
   `HttpResponseHeadReader.StatusLineReported` hook. That is the only place that knows the status line
   has just been written and whether a body was sent.
2. `ClientReaderResetTraceEvents` turns that line into the three `[READ]` lines: it writes the
   needs-rewind line before it, then remembers the rewind so that the hop's reset is the
   `will rewind reader` line and the next `Issue another request` comes after the `rewind readers` line.
   It is only in the event chain under `read` or `all`, so plain `-v` writes the plain line alone.

## Consequences

- The `[READ]` lines stay with the other `[READ]` reset lines in `Curl.Console`, and the HTTP library
  writes only the plain `-v` line it shares with every verbosity.
- The rule is "any `3xx` under `-L`", as curl decides at the status line. A `304` with a `Location` was
  seen to do the same in one run against the recorder, which hung on it, so that case is not pinned.

## Alternatives considered

- Writing the `[READ]` lines in the HTTP library: it does not see the hop's end or the
  `Issue another request` line, which `Curl.Core` writes, so it could not place two of the three.
