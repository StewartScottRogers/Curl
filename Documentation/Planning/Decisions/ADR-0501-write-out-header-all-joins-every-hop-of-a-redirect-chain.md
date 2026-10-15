# ADR-0501: -w %header{name:all:separator} joins every hop of a redirect chain

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2267
- Decided by Claude under Stewart's delegation

## Context

curl 8.22.0's `-w` reads `%header{name:all:separator}` as every value of the header joined by
the separator, and `\}` inside the braces as a literal `}`. Upstream case 764 follows a 301 that
sends `This: one` and `This: two` to a 200 that sends `This: three` and `This: four`, and expects
`one***two***three***four`: the values of every response in a `-L` chain, not only the last.
Case 765 expects `%header{this:all:-{\}-}` to write `one-{}-two-{}-three-{}-four`.

`TransferReport.ResponseHeaders` held only the last response's headers, and
`RedirectFollower` returned the last hop's report, so the earlier hops' headers were lost.

## Decision

- `TransferReport` gains `EarlierResponseHeaders`: every header of every response before the
  last in a `-L` chain, hop by hop, in the order received. `RedirectFollower`'s merged report
  fills it; without `-L` it stays empty.
- `IWriteOutVariableSource.FindAllHeaderValues` returns the earlier hops' values and then the
  last response's, each trimmed of spaces and tabs. `%header{name}` without `:all:` keeps
  reading the last response alone, as curl's `curl_easy_header(..., -1, ...)` does.
- `WriteOutTemplateRenderer` ends a `%header{` at the first `}` not preceded by `\`, splits the
  content at the first `:all:` and replaces `\}` with `}` in the separator; any other backslash
  stays as written. The 256-byte name limit applies to the whole content, as curl's `hname`
  buffer does.

## Consequences

- Upstream cases 764 and 765 pass and are listed.
- `%header{name}` whose name contains `\}` now reads past that brace, as curl's scan does.
