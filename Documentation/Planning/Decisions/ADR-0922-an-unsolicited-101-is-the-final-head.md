# ADR-0922: An unsolicited 101 Switching Protocols is the final head

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3599)
- Relates to: [ADR-0050](ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md)

## Context

CGR-070: Curl read every 1xx head as an interim head, so after `HTTP/1.1 101 Switching\r\n\r\nhello`
it expected another status line and failed with exit 8, `Invalid status line`, writing no body.
Measured on 2026-10-26 with `Record-CurlExchange.ps1` against curl 8.22.0 (the reference build Git
for Windows ships), real curl takes a `101` that no h2c upgrade asked for as the final head:

- `HTTP/1.1 101 Switching\r\n\r\nhello`, then close: exit 0, stdout `hello`; `-w %{http_code}` is `101`.
- The same with `Content-Length: 2`: stdout `hello`. The body runs to the close and the
  Content-Length frames nothing, as curl reads a 1xx head's headers as bodyless (`http_bodyless`).
- `HTTP/1.0 101` and a 101 carrying `Upgrade: h2c` that was not asked for behave the same way.
- With `-i`, the 101 head is written and then the body.
- A 101 whose body is empty (closed right after the head, with or without `Content-Length: 0`)
  fails with exit 52, `Empty reply from server`, after the head is written to `-i`'s output.
- `-v` writes no `no chunk, no close, no size. Assume close to signal end` line for it.

## Decision

- `HttpResponseHeadReader` takes a 1xx head as interim, except a `101` when the request did not
  ask to upgrade to h2c (`AsksH2cUpgrade`, set when the exchange runs on an
  `HttpH2cUpgradeConnection`) and the heads are not an HTTP/2 or HTTP/3 stream's. That 101 is the
  final head and the bytes after it are its body.
- `HttpResponseBodyReader.FramingHeadersOf` gives no framing headers for a final 101, so its body
  is read to the close without Content-Length, Transfer-Encoding or Content-Encoding, and no
  refusal is found in them. A body that ends without a byte fails with exit 52.
- `HttpProtocolHandler` writes no "Assume close" line for a final 101.
- The connection is still never reused after a 101 (ADR-0050), so `-v` ends with
  `shutting down connection` where curl 8.22.0 writes `left intact`; that `-v` difference is
  left to its own task.

## Consequences

The CGR-070 repros give curl's exit code, output and request bytes. An h2c upgrade still treats
`HTTP/1.1 101` as the interim head before stream 1's response.
