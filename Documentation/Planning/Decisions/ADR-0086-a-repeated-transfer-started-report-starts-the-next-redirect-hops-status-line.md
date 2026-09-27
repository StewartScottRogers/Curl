# ADR-0086 — A repeated "transfer started" report starts the next redirect hop's status line

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-277.

## Context

Under `-L`, curl 8.21.0 draws the progress meter's status line once per hop. Measured on
2026-09-27 with `Record-CurlExchange.ps1` (mingw build, Schannel), `/a` answering
`302 Found` to `/b` with an empty body and `/b` answering `200 OK` with `hello`:

- `curl -L -i http://127.0.0.1:18246/a` wrote the two header lines, then for `/a` the
  zero line drawn three times, `\r`-separated, and CRLF, then for `/b` the zero line and
  the done line three times, and CRLF. A chain of two redirects drew one such line per
  hop. On one run of four a hop drew a further line; curl draws on a timer, so a slow hop
  draws more, and the tests pin the fast case.
- `curl -L --max-redirs 0 -i ...` wrote the header lines, the redirect hop's line (zero
  three times, CRLF), then `curl: (47) Maximum (0) redirects followed`.

`RedirectFollower` (in `Curl.Core`) gives every hop the first hop's
`ITransferContext.Progress`, and says nothing to the sink when a hop ends. ADR-0045 says a
handler reports `ReportTransferStarted()` at most once per transfer and "the consumer
ignores a repeat". Each hop is its own dispatch, so the HTTP handler reports "started"
once per hop, on the same sink.

## Decision

`Curl.Console`'s `TransferProgressRecorder` reads a repeated `ReportTransferStarted()` as
the next hop starting. It finishes the hop before with two done draws (whatever bytes it
reported), ends it with a newline, sets every counter and speed sample back to zero, and
draws the new hop's zero line. The final hop is finished as any transfer is. When the
transfer ends with exit 47 (`--max-redirs` refused the redirect), the hop that asked for
it gets the same two done draws instead of a failure's.

No contract changes: the repeat is still harmless to every other consumer, and no
handler reports "started" twice within one hop.

## Consequences

- The meter's bytes match curl's for a followed chain and for exit 47, with no change
  to `Curl.Core` or `Curl.Protocol.Abstractions`.
- ADR-0045's "the consumer ignores a repeat" no longer describes `Curl.Console`'s
  consumer; it still describes what a handler may rely on.
- A hop that fails before connecting (and so never reports "started") leaves the hop
  before it open; the failure's draws land on that hop's line. curl's bytes for that case
  are not measured.

## Alternatives considered

- **A hop callback on `RedirectFollower` or a per-hop sink on the context.** Rejected for
  now: it changes `Curl.Core` or the shared contract for a signal the recorder already
  receives, and would make this task run apart from every protocol task.
- **Counting the hops from `TransferResult.Report`.** Rejected: the report arrives only
  after the last hop, too late to split the lines the recorder has already drawn.
