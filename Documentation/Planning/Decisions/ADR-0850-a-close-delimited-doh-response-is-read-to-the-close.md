# ADR-0850: A close-delimited DoH response is read to the close

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3689)
- Relates to: ADR-0152 (its BL-641 amendment), CGR-160 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

ADR-0152's BL-641 amendment measured a DoH response framed by neither `Content-Length` nor chunked
coding (body ending at the close) as `DoH request Failure when receiving data from the peer` and
exit 6, so `DohResponseReader` refused every such body. CGR-160 found curl 8.22.0 reading that body
to the close and resolving the name, for an HTTP/1.0 answer as well as an HTTP/1.1 one.

BL-3689 measured curl 8.22.0 Schannel on Windows with a C# loopback DoH server answering
`HTTP/1.0 200 OK` or `HTTP/1.1 200 OK`, `Content-Type: application/dns-message`, no
`Content-Length`, then the A or AAAA answer and a TCP shutdown
(`curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`): exit 0, the name
resolved, for both versions. Over TLS (`--doh-insecure --doh-url https://127.0.0.1:21801/dns-query`,
HTTP/1.0, no `Content-Length`) curl resolved the name when the server sent `close_notify` before
closing, and failed with exit 6 when it closed the TCP connection without one. BL-641's row was a
TLS server closing without `close_notify`: the failure is the truncated TLS stream, not the framing.

## Decision

`DohResponseReader` reads a body framed by neither `Content-Length` nor chunked coding up to the
peer's close and hands it on as any other body; one that runs past curl's 3000-byte DoH buffer is
still a receive failure. A TLS connection closed without `close_notify` already reads as a failure
in the TLS layer, as curl's does, so that case still exits 6. This replaces the close-delimited row
of ADR-0152's BL-641 amendment.

## Consequences

The plain-HTTP and TLS repros of CGR-160 give curl's exit code and output. `Cgr160Tests` pins the
reader; `DohDnsResolverTraceTests` uses a body cut short of its `Content-Length` for its receive
failure lines.
