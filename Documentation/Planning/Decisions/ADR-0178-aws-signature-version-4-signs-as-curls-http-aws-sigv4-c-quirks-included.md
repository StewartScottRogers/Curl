# ADR-0178 — AWS Signature Version 4 signs as curl's `http_aws_sigv4.c` does, quirks included

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-628.

## Context

`--aws-sigv4 "provider1[:provider2[:region[:service]]]"` makes curl sign each HTTP request
with AWS Signature Version 4. A server checks the signature byte for byte, so a drop-in
replacement must build the same canonical request as curl, including where curl departs from
AWS's reference. curl 8.21.0 (mingw, Schannel) was measured against
`Record-CurlExchange.ps1`; the commands and the headers it sent are in BL-628's notes. The
edge cases were read from curl 8.21.0's `lib/http_aws_sigv4.c` and each one that changes the
signature was measured. Wiring the signer into a transfer is BL-629.

## Decision

1. **Where it lives.** `Curl.Authentication.UnitLibrary`: `AwsSigV4Signer` (with
   `TimeProvider` and the argument `Encoding` injected, BCL `SHA256` and `HMACSHA256`) signs
   an `AwsSigV4Request` - the parts of the request curl holds when it signs: method, URL host
   name, the `Host` header value, path and query as curl's URL parser leaves them, the `-H`
   headers as given, whether the request is a GET or HEAD, the in-memory POST fields and the
   `-T` file size - into an `AwsSigV4SigningResult`: the header lines to send, or curl's exit
   code and message. `AwsSigV4Scope`, `AwsSigV4Headers` and `AwsSigV4UriEncoding` hold the
   parameter, header and URI rules.
2. **The parameter.** Empty means `aws:amz`. A part is read up to the next `:`; an empty or
   over-64-byte part reads as missing: a missing first provider is exit 43 `first aws-sigv4
   provider cannot be empty`; a missing second provider makes it the first and ends the
   parameter; a missing region ends it. With no service, the host's first label is the
   service and (with no region) its second the region, each needing a `.` after it; else exit
   3 `aws-sigv4: service missing in parameters and hostname` or `... region missing ...`.
   `127.0.0.1` so signs for service `127` in region `0`, as curl does.
3. **Names.** The algorithm and signing-key prefix are provider 1 uppercased
   (`OSC4-HMAC-SHA256`, `OSC4<secret>`), the request type lowercased (`osc4_request`). The date
   header curl sends is `X-` + provider 2 with only its first letter uppercase + `-Date`; the
   content hash header keeps provider 2's case as given (`x-Amz-content-sha256`).
4. **Headers.** Signed: curl's `Host` (unless a custom one), the content hash header, and every
   `-H` header except a bare `Name:`, one of blanks only, or one with no `:` or `;`
   (`Name;` signs as `name:`). Names lowercase, values trimmed with inner blank runs made one
   space, sorted by name stably, same names merged with `,`. A custom `X-<Provider>-Date` or,
   failing that, `Date` header replaces curl's: its value's leading 16 letters and digits are
   the timestamp, anything else signs an empty date; one written `Name;` is exit 27
   `Out of memory`, as curl's code path gives. A custom `Authorization` header means nothing
   is signed; `--path-as-is` is exit 43.
5. **Payload.** A custom `x-<provider2>-content-sha256:` value is the payload hash. Otherwise,
   only for provider `aws` and service `s3` (any case) curl adds that header: the SHA-256 of
   the body when curl knows it (GET, HEAD, an empty upload, in-memory POST fields), else
   `UNSIGNED-PAYLOAD`. Other services sign the hash of the POST fields, or of nothing.
6. **URI.** For `s3`, `s3-express` and `s3-outposts` (case-sensitive) the path is signed as
   given; for any other service each byte but an unreserved one or `/` is percent-encoded
   again (`%20` signs as `%2520`). The query is split on `&`, empty components dropped, each
   key and value percent-decoded and re-encoded (a decoded `+` stays `%2B`, a literal `+`
   becomes `%20`), sorted by key then value with empty keys first in their original order,
   and joined as `key=value`. An empty key is written `(nil)`, because curl's key buffer is
   never allocated and its printf writes a null `%s` that way; measured, it changes the
   signature. 128 components or more is exit 100 `HTTP request too large`.

## Alternatives considered

- **Follow AWS's reference canonicalization instead of curl's.** Lost: the drop-in rule is to
  match curl; a script that works against a server with real curl must work with ours. Where
  both agree (AWS's published examples) the tests check ours against AWS's values too.
- **Leave out the `(nil)` key and the exit 27 path as curl bugs.** Lost: both change what goes
  on the wire or the exit code, and were measured.
- **Unstable sort for equal query keys**, as the C runtime's `qsort` may be. Lost: the
  measured case (`z=1&=b&=a`) keeps the original order, which a stable sort gives always.

## Consequences

- BL-629 maps `--aws-sigv4`, `-u`, `-H`, `-d`, `-T` and `--path-as-is` onto
  `AwsSigV4Request` and adds `HeaderLines` after the `Host` header, or ends with the result's
  exit code and message.
- The string to sign is exposed for `-v`'s `aws_sigv4: String to sign` line.
