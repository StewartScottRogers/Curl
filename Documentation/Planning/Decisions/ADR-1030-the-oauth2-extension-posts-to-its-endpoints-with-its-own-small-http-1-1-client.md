# ADR-1030: The OAuth2 extension posts to its endpoints with its own small HTTP/1.1 client

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4213)

## Context

The OAuth2 extension must POST a form to a token or device authorization endpoint and read the JSON
response, including a 400 or 401 that carries the error response (RFC 6749 §5.2). An extension may
reference neither `Curl.Protocol.Http` nor another extension (ADR-0999), so it cannot use curl's
HTTP code or the S3 extension's client (ADR-1018).

## Decision

`Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpClient` opens a connection through the injected
`IConnector` (`ConnectTarget.UseTls` for `https`, the host in its IDNA form), sends one request and
reads the whole response:

- The request is `POST` to the endpoint's path and query with `Host`, `Accept: application/json`,
  `Content-Type: application/x-www-form-urlencoded`, any `Authorization`, `Content-Length` and
  `Connection: close` (RFC 9112 §3, §9.6). A token exchange is one request, so the connection is
  never reused and never marked reusable.
- The response head is read as ADR-1018 reads it (obs-fold rejected, lines up to 64 KiB). Interim 1xx
  responses are skipped (RFC 9110 §15.2). The body is framed by `Content-Length`, the chunked coding
  (trailers discarded) or the close (RFC 9112 §6.3, §7.1), read whole up to 1 MiB and decoded as
  UTF-8 (RFC 8259 §8.1), whatever the status.
- Every failure is an `OAuth2HttpResult` with an `OAuth2HttpFailure` and the exit code curl gives the
  same failure: a non-HTTP endpoint 1, a failed connect the connector's own code, an empty reply 52,
  a malformed head, length or chunk 8, a connection closed mid-response 18, a body over 1 MiB 63, an
  I/O error 56, an `Authorization` value holding CR or LF 43. Only cancellation escapes as an
  exception.

The client duplicates the S3 extension's small reader rather than sharing it, because extensions
may not reference each other and the reader is too small to justify a shared layer.

## Consequences

The flows (BL-4161) get a response or a typed failure from one call and can be tested over recorded
bytes. Proxies, redirects and HTTP/2 for token requests are not supported; a later task adds them if
an identity provider needs them.
