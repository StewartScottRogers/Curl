---
id: BL-2182
title: Sign HTTP requests with the --httpsig-* options through an authenticator in Curl.Console, as curl 8.22.0's HTTPSIG auth does
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2180]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests, Documentation]
requirement: none
created: 2026-10-13
completed:
---
# BL-2182 — Sign HTTP requests with the --httpsig-* options through an authenticator in Curl.Console, as curl 8.22.0's HTTPSIG auth does

## Goal

An HTTP request sent with BL-2180's parsed `--httpsig-algo`, `--httpsig-key`, `--httpsig-keyid`
and `--httpsig-headers` carries curl 8.22.0's `Signature-Input` and `Signature` headers, signed
through `Curl.HttpMessageSignatures.UnitLibrary`'s `CurlHttpSignature.CreateHeaders`.

## Context

Split out of BL-2180 on 2026-10-13: BL-2180 parses the four options into
`CommandLineOptions.HttpSignatureAlgorithm`, `HttpSignatureKey`, `HttpSignatureKeyId` and
`HttpSignatureHeaders` (each verbatim, an empty value refused as blank) and adds them to
`--help`, the manual and `--ai-help`; this task sends them. ADR-0485 holds the decision to
implement them; upstream facts read at `curl-8_22_0`:

- **Setup checks, per HTTP transfer** (`src/config2setopts.c`, `httpsig_setopts`, run from
  `http_setopts`): when any of the four is given, a missing key is `curl: --httpsig-key is
  required` and a missing key id `curl: --httpsig-keyid is required`, then an algorithm other
  than `ed25519` or `hmac-sha256` (case-insensitive; absent means `ed25519`) is
  `curl: --httpsig-algo: unsupported algorithm '<value>'`, each exit 2 (`CURLE_FAILED_INIT`,
  printed by `errorf`, so no `curl: (2)` line; hidden by `-s` without `-S`). A key starting `@`
  names a file read as text: one that cannot be opened is `curl: httpsig: cannot open key file
  '<name>'` exit 26, an empty one `curl: httpsig: key file '<name>' is empty` exit 43. Upstream
  tests 5008 (exit 2), 5009 (exit 2) and 5010 (exit 26) pin the codes.
- **Auth slot** (`lib/http.c`, `output_auth_headers`): `CURLOPT_HTTPSIG_ALGORITHM` sets the
  origin's auth to HTTPSIG alone (`lib/setopt.c`), in place of every other scheme, never for a
  proxy, as `--aws-sigv4` does - so model it on `Curl.Console/AwsSigV4HttpAuthenticator.cs`
  and `HttpAuthRequest.AwsSigV4`: a new inputs record on `HttpAuthRequest`, filled by
  `HttpProtocolHandler` from `HttpRequestOptions`, and a Console authenticator that calls
  `CurlHttpSignature.CreateHeaders` with `created` from the injected `TimeProvider` and returns
  `Signature-Input` then `Signature` as header lines in the `Authorization` slot (right after
  `Host`, before `User-Agent`; upstream test 5001). It signs only when
  `Curl_auth_allowed_to_host` holds: a redirect to another host or port drops both headers
  unless `--location-trusted` (tests 5023 to 5025; a same-origin hop is signed again for its own
  path and query, test 5024).
- **Verbose**: `* httpsig: Signature-Input params: <params>`, `* httpsig: Signature base:
  [<base>]`, `* httpsig: Signature-Input: sig1=...`, `* httpsig: Signature: sig1=:...:`, then
  `* Server auth using HTTPSIG with user '<user or empty>'` - the last also when a `-H` line sets
  `Signature` or `Signature-Input` and nothing is signed (test 5017).
- **Transfer-time failures**: every `HttpSignatureException` ends the transfer with exit 43 and
  `curl: (43) <its message>` (tests 5019, 5020).
- `@authority` is the `Host` header's value when one is sent (an `-H Host:` included), else the
  host plus `:port` when not the scheme's default; header components come from `-H` lines only.
- Upstream cases 5000 to 5025 and the keys `tests/data/data-httpsig-ed25519.key` and
  `data-httpsig-hmac-sha256.key`: unskip the ones Curl.Conformance.UnitTests can run (debug
  curl fixes `created` to 0 through `CURL_FORCETIME` and `CURL_HTTPSIG_CREATED`).

## Acceptance criteria

- [ ] An HTTP request sent with the options carries `Signature-Input` and `Signature` in the
      `Authorization` slot; a test in `Curl.Protocol.Http.UnitTests` or `Curl.Console.UnitTests`
      checks the header bytes for a fixed `created` time through an injected `TimeProvider`, for
      ed25519 and hmac-sha256 (upstream test 5001's and 5002's signatures with `created=0`), and
      that a `-H Signature:` line suppresses both.
- [ ] A missing key or key id and an unknown algorithm end with exit 2 and upstream's
      `config2setopts.c` text; an unreadable `@file` key with exit 26; a bad hex key, a duplicate
      or unknown component with exit 43 and the library's `httpsig: ...` message.
- [ ] A redirect to another origin is sent unsigned without `--location-trusted`.
- [ ] `-v` prints upstream's four `httpsig:` lines and `Server auth using HTTPSIG with user ''`.
- [ ] Every changed library stays at 100% line and branch coverage, complexity at most 10, CRAP
      at most 30.

## Notes

## Log

- 2026-10-13: Created.
