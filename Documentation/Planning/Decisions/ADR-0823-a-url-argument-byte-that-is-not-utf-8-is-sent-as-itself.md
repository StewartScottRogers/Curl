# ADR-0823: A URL argument byte that is not valid UTF-8 is sent as itself

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3851)
- Relates to: [ADR-0821](ADR-0821-linux-and-macos-read-argv-raw-bytes-with-surrogate-escapes.md), [ADR-0697](ADR-0697-url-bytes-that-are-not-utf-8-are-lone-surrogates.md)

## Context

CGR-016 part 3 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Since ADR-0821 an argument
byte b that is not valid UTF-8 reaches Curl off Windows as the lone surrogate U+DC00 + b, the same
escape `UrlByteText` uses for a URL's bytes. The HTTP request target still encoded the path by runes
and the query as UTF-8, so a lone surrogate became U+FFFD's EF BF BD. Measured with
`Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1` against curl 8.18.0 (OpenSSL,
Ubuntu): `curl -s $'http://u\xff:p\xff@172.26.96.1:18851/a\xff?q\xff#f\xff'` sends
`GET /a%FF?q` and the raw byte FF, and `Authorization: Basic df86cP8=` (u FF : p FF). A host holding
the byte (`http://a\xff.example/`, also under `--resolve`) fails with exit 3.

## Decision

- `CurlUrl.RequestPath` is the path with each of its `UrlByteText.ToBytes` bytes above `~` as `%XX`:
  a character is its UTF-8 as before, an escaped byte is itself. `HttpUrlText.Path` is that.
- `HttpUrlText.RequestTarget` writes the query's `UrlByteText.ToBytes` bytes, so the escaped byte goes
  out raw, as curl sends the query's bytes unchanged.
- Credentials already go as their bytes (ADR-0821, CGR-052). A host with an escaped byte is already
  refused by the URL parser, as curl refuses it, so nothing changes there.
- Windows hands over UTF-16 arguments, which hold no lone surrogate from the command line, so every
  Windows URL and every valid UTF-8 URL encodes as before.

## Consequences

The bytes `curl` sends for a path or query are the bytes the user passed, on every platform, as curl
never re-encodes its arguments.
