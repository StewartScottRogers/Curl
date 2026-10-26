# ADR-0823: -X methods and -b strings go out as their argv bytes

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3594)
- Relates to: [ADR-0067](ADR-0067-command-line-header-text-becomes-bytes-in-the-platform-curls-encoding-inside-the-http-formatter.md), [ADR-0821](ADR-0821-linux-and-macos-read-argv-raw-bytes-with-surrogate-escapes.md)

## Context

CGR-065: on Linux curl 8.18.0 copies `-X` and `-b name=value` from its `argv` unchanged, so
`-X 中` sends `E4 B8 AD /a HTTP/1.1`, `-b c=中` sends `Cookie: c=E4 B8 AD` and `-b c=é` sends
`Cookie: c=C3 A9` (measured 2026-10-26 under WSL against a netcat listener). Curl put both into the
request head as UTF-16 characters and turned the head into bytes with Latin-1, so it sent `?` and
`E9`. `-H` values already went out in `HttpRequestOptions.CommandLineTextEncoding`, the platform's
argument encoding (`CredentialEncoding.ForPlatform`: UTF-8 on Linux and macOS, the ANSI code page on
Windows, which matches the Schannel build's ANSI `argv`).

## Decision

- `HttpRequestHeadFormatter` writes the request line's method as its bytes in
  `CommandLineTextEncoding`, one character per byte, as it writes `-H` values.
- The `Cookie` value mixes the stored cookies, which the cookie store reads from files and
  responses one character per byte, with the `-b` strings, so the formatter cannot encode it whole.
  `CookieEngine.FromHttpOptions` takes the encoding instead and turns the joined `-b` strings into
  their bytes, one character per byte, before they reach the store; `CurlComposition.SharingRunCookies`
  passes the platform's encoding. The too-long check counts bytes in that encoding.
  A library handle (`Libcurl`, `Curl.Api`) passes none and keeps the strings as they are.

## Consequences

Linux and macOS send the argument's UTF-8 bytes; Windows still sends its ANSI bytes (`?` where the code
page has no character), as curl's Schannel build does. An `argv` byte that is not valid UTF-8 (a lone
surrogate, ADR-0821) in a `-b` string is not yet given back as that byte; a follow-up task covers it.
