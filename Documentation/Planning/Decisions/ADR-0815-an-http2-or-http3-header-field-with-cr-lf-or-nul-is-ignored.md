# ADR-0815: An HTTP/2 or HTTP/3 response header field whose value holds a CR, LF or NUL is ignored

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3540 (CGR-011)
- Decided by Claude under Stewart's delegation.

## Context

`Http2ResponseHead` writes each HTTP/2 or HTTP/3 header block as the HTTP/1 head bytes the
shared header parser reads (ADR-0159, ADR-0144). It wrote every non-pseudo field as
`name: value\r\n` unchecked, so a value holding `\r\n` injected header lines (a `Set-Cookie`
reached the cookie jar and the next request), and a lone LF or NUL failed the transfer with
exit 8. RFC 9113 section 8.2.1 makes such a field malformed.

Measured on 2026-10-26 with curl 8.18.0 (nghttp2 1.68.0, WSL) against canned h2c responses
from `nc`: a single-URL transfer whose response carries `x-inj: a\r\nset-cookie: evil=1; Path=/`,
`a\r\nb`, `a\nb` or `a\0b`, or a 302 whose `location` holds CRLF (with `-L`), exits 0 with no
message, writes `HTTP/2 200 ` and the empty line to `-D -` with the field left out, delivers
the body, leaves the jar empty and does not follow the redirect.

## Decision

`Http2ResponseHead` leaves out of the head and trailer bytes any field whose value holds a CR,
LF or NUL, without a message, as nghttp2 does for curl. HTTP/3 heads take the same path, as
curl's nghttp3 layer also rejects such values. Other RFC 9113 value checks (leading or
trailing whitespace) are outside CGR-011 and unchanged.

## Consequences

No server can inject header lines across the HTTP/2 or HTTP/3 to HTTP/1 boundary, and the
four repros exit 0 as curl does. `%{num_headers}` no longer counts the dropped field.
