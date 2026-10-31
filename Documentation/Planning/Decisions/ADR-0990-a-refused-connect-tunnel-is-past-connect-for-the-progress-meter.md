# ADR-0990: A CONNECT the proxy refused is past connect for the progress meter

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-3771)

## Context

CGR-242: without `-s`, curl 8.22.0 writes the progress meter's header and a zero row before
`curl: (7) CONNECT tunnel failed, response 403`. Curl wrote only the error line, because the
HTTP handler reported the transfer started only once a connection was open, and the runner
draws the meter of a failed transfer only after that report (BL-130).

## Decision

`HttpProtocolHandler` reports the request started when a failed connect carries a
`ProxyConnectResponseCode`, that is, when the proxy answered the CONNECT. A connect that fails
earlier (refused, unresolved) still reports nothing.

Measured 2026-10-29 with curl 8.22.0 (Schannel) on Windows: a refused CONNECT and an SMTP
`RCPT` refused with 550 both write the header and a zero row. Curl already did so for SMTP and
POP3 (their handlers report started after connect), so only the proxy case changed. The
Linux-only meter on a plainly refused connect (WV-21) is left as it is: Windows curl writes
none there, and Curl matches the Windows build.

## Consequences

`Cgr242Tests` in `Curl.Console.UnitTests` and `Curl.Original.Protocol.Http.UnitTests` pin it.
