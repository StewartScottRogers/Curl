# ADR-0821: The origin-form path's percent-escapes are sent in upper case

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3583, CGR-054)

## Context

Third-party gap report finding CGR-054 found that Curl sends a URL path's lower-case percent-escapes
as typed, where curl 8.22.0 upper-cases their hex digits. Measured on 2026-10-26 with
`Record-CurlExchange.ps1` against Git for Windows' curl 8.22.0 (Schannel):

| Command | Request line real curl sent |
| --- | --- |
| `curl -sS http://127.0.0.1:P/a%2fb%7e` | `GET /a%2Fb%7E` |
| `curl -sS --path-as-is http://127.0.0.1:P/a/%2e%2e/b` | `GET /a/%2E%2E/b` |
| `curl -sS http://127.0.0.1:P/a%2fb%c3%bc?x=%2f` | `GET /a%2Fb%C3%BC?x=%2f` (query as typed) |
| `curl -sS http://127.0.0.1:P/a%zz%2/b%a` | `GET /a%zz%2/b%a` (no two hex digits, as typed) |
| `curl -sS -w %{url_effective} http://127.0.0.1:P/a%2fb` | `GET /a%2Fb`, and prints `.../a%2fb` |
| `curl -sS -x http://127.0.0.1:P http://example.com/a%2fb` | `GET http://example.com/a%2fb` (absolute form as typed) |
| `curl -sS ws://127.0.0.1:P/a%2fb%3d` | `GET /a%2Fb%3D` |
| `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H "X-Amz-Date: 20250101T000000Z" http://127.0.0.1:P/a%2fb%c3%bc` | `GET /a%2Fb%C3%BC`, signed over that path |
| `curl -sS --digest -u u:p http://127.0.0.1:P/a%2fb?q=%2f` | `uri="/a%2Fb?..."` in the Digest answer |

curl 8.18.0 (OpenSSL, Linux) sends the path as typed; the target is curl 8.22.0 on every platform.

## Decision

1. `Curl.Protocol.Abstractions`' `UrlPathEscapes.WithUpperCaseHexDigits` upper-cases the hex digits of
   each `%` followed by two hex digits and leaves anything else as it is.
2. The HTTP handler's origin-form target (`HttpUrlText.OriginTarget`: the request line, the HTTP/2 and
   HTTP/3 `:path` taken from it, and the target authentication answers with), the WebSocket upgrade's
   target and the AWS SigV4 canonical path use it on the path only. The query, the absolute form a
   forward proxy gets, `%{url_effective}`, `-O` file names and `CurlUrl.AbsolutePath` itself keep the
   escapes as typed, so the parser does not change.

## Consequences

The request bytes and SigV4 signatures of every CGR-054 repro equal curl 8.22.0's on Windows. A Digest
retry after a 401, where curl 8.22.0 also upper-cases the query's escapes, is left to a follow-up task.
