# ADR-0923: A redirect URL carries an absolute `Location`'s header bytes marked

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3867, CGR-062)
- Relates to: [ADR-0893](ADR-0893-write-out-header-values-keep-their-raw-bytes.md)

## Context

`HttpRedirectLocation` reads a `Location` value as Latin-1, one character per byte, and keeps an absolute
value's bytes, so `Location: http://\xE9.invalid/b` became `TransferReport.RedirectUrl` holding U+00E9, which
`-w` wrote as UTF-8 (`C3 A9`). Measured on 2026-10-26 with `Record-CurlExchange.ps1`, curl 8.22.0 Schannel,
`-w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n'`: without `-L` exit 0 and
`302 0 http://127.0.0.1:P/a/ http://<E9>.invalid/b`; with `-L` exit 6, `302 1 http://<E9>.invalid/b ` and
`curl: (6) Could not resolve host: xn--9ca.invalid`. curl writes the header's byte unchanged.

Marking the whole string at write-out time, as `content_type` is (ADR-0893), is wrong: a relative `Location`
keeps the request URL's authority, which is command-line text such as the IDN host `åäö.se` (upstream tests
2046 and 2047) and must stay UTF-8. Carrying a separate span or flag beside the URL would add a field to
`TransferReport`, `TransferInfo`, the libcurl info readers and the console's easy-handle read-back.

## Decision

- `Curl.Protocol.Abstractions.HeaderByteText` marks a header byte b from 0x80 to 0xFF as U+F700 + b, the same
  marking `WriteOutTemplateRenderer.MarkHeaderBytes` uses and the renderer already writes as the raw byte;
  `Unmark` and `HasMarks` undo and detect it.
- `HttpRedirectLocation.Resolve` marks the result of an absolute (scheme-bearing) `Location`, which is header
  text throughout. A relative or scheme-relative one is percent-encoded before resolving, so it holds no header
  byte, and its authority (the request URL's) is never marked.
- `RedirectFollower` unmarks the target before checking, logging, HSTS-switching and following it, so it is
  parsed and followed exactly as before, and marks the effective URL it becomes again when the target held marks.
- `TransferWriteOutVariables` unmarks before parsing a `urle.*` part. `redirect_url` and `url_effective` need
  no change: the marks travel through `TransferInfo`, the libcurl info strings and the console's read-back to the
  renderer, which writes each as its byte.

## Consequences

- `%{redirect_url}` and `%{url_effective}` write an absolute `Location`'s raw bytes as curl does; a URL from the
  command line is written as UTF-8 as before.
- Libcurl's `CURLINFO_REDIRECT_URL` and `CURLINFO_EFFECTIVE_URL`, and `Curl.Api`'s `TransferInfo`, hand such a
  URL out with its marks (U+F7E9 where they gave U+00E9) only when the `Location` held a byte from 0x80;
  every other URL is unchanged. BL-3871 turns them into the libcurl byte text (`UrlByteText`) those readers
  should give.
