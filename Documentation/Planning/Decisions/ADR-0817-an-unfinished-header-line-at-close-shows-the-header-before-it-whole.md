# ADR-0817: An unfinished header line at close shows the header before it whole

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3556, third-party gap report CGR-027)

## Context

curl acts on a response header only once a byte of the next line shows it whole (BL-483), so a
head the peer closes right after a header's line end leaves that last header unacted on. Curl
applied that rule to every head that ended at a close, also when the peer had sent part of a
next line: `HttpLineReader` drops the unfinished bytes at close, so `HttpResponseHeadReader`
saw a head ending on `Content-Length: 3\r\n`, left the Content-Length out of the head acted on,
and exited 0. Real curl 8.22.0 (Schannel) exits 18, `transfer closed with 3 bytes remaining to
read`, for `HTTP/1.1 200 OK\r\nContent-Length: 3\r\nX` and then close, also with `-f` on a 404.
When the unfinished line is a lone `\r`, curl also writes that `\r` to `-i`'s output after the
head (37 bytes, not 36).

## Decision

At close, `HttpResponseHeadReader.EndHeadAtClose` takes the unfinished bytes and, when they show
the pending header whole (`ShowsPendingHeaderWhole`, the test that already decides when a header
can be refused), acts on the whole head: the last header frames the body, is told to
`HeaderReceived` and counts for refusals. `HttpResponseHeadBuilder.EndHeadAtClose` keeps an
unfinished line that is exactly `\r` in the head's bytes, so `-i` and `-D` write it as curl does.
Any other unfinished bytes are still neither written nor parsed, as curl does (measured: the
`X-Foo: ba` of the report's `/closehdr` is not written).

## Consequences

Every repro of CGR-027 now matches curl 8.22.0 in exit code, stderr, stdout and request bytes
(BL-3556 Notes); a head closed with no Content-Length still succeeds. Whether curl writes other
unfinished lines that begin with `\r` (such as `\rX`) was not measured and stays unwritten.
