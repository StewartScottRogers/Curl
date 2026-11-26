# ADR-1428 — `--doh-url` sends the A and AAAA queries on one HTTP/2 connection

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1424, which gave each query a connection of its own over HTTP/2

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6038.

## Context

curl sets `CURLOPT_PIPEWAIT` on its DoH sub-transfers. Measured 2026-11-26 with curl 8.18.0
on Ubuntu (OpenSSL, nghttp2) against Unbound 1.26.2's DoH, `-v --trace-config doh`:

- While the first connection is being made the second sub-transfer writes `Connection #1 is not
  open enough, cannot reuse`, `Found pending candidate for reuse and CURLOPT_PIPEWAIT is set` and
  `Waiting on connection to negotiate possible multiplexing.`
- The server picks `h2`: the A query goes on stream 1, then `Multiplexed connection found`,
  `Reusing existing https: connection with host 127.0.0.1`, and the AAAA query on stream 3.
- `Connection #1 to host ... left intact` comes once, as the last stream ends, and the transfers
  after it count one DoH connection (the second URL's connection is `#2`).
- A second name looked up in the same run goes on streams 5 and 7 of the same connection.
- Against an HTTP/1.1 server (nginx) the second query waits and then reuses the first connection.

## Decision

- Over TLS, with both address queries to send, the resolver opens one connection first. If it
  picked `h2`, A goes on stream 1 and AAAA on stream 3, with the lines above, and one `%{conn_id}`
  is taken. Each query's `DoH request complete, K to go` (or error line) follows the end of its
  stream. A reset or over-long stream fails its own query; a failed connection fails every query
  still unanswered.
- Any other connection carries one query and the other opens its own, as before (ADR-0152,
  ADR-0380): traced, AAAA first; untraced, both exchanges at once. The second connect now starts
  after the first has finished.
- The waiting lines are written under every platform's build, since each offers `h2` (ADR-1424),
  and whatever the server then picks. A connect through a proxy was not measured and gets none.
- A plain `http://` DoH URL is unchanged.

## Consequences

Reusing the connection for a later name, an HTTPS-record query, or an HTTP/1.1 server's second
query is left to a task of its own: the resolver still closes its connection once a name's queries
are answered.