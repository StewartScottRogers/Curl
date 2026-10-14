# Curl.HttpMessageSignatures.UnitLibrary

RFC 9421 HTTP Message Signatures as curl 8.22.0's experimental `--httpsig-*` options sign a
request (upstream `lib/http_httpsig.c` at `curl-8_22_0`), hand-built against the BCL and
`Curl.Cryptography.UnitLibrary`'s Ed25519 (ADR-0484). Pure computation: no I/O and no clock;
the caller passes `created`.

Namespace `Curl.HttpMessageSignatures`. It holds:

- `CurlHttpSignature`: `CreateHeaders`, the whole of curl's signing step - option checks, key
  decoding, component parsing, the `sig1` `Signature-Input` and `Signature` values - or `null`
  when a `-H` line already sets either header.
- `HttpSignatureComponents`: the `--httpsig-headers` parser and the default component set.
- `HttpSignatureKey`: the hex key decoder.
- `HttpSignatureBase`: the signature parameters and signature base (RFC 9421 sections 2.3, 2.5).
- `HttpMessageSigner`: Ed25519 or HMAC-SHA256 over the base's UTF-8 bytes.
- `HttpSignatureRequest`, `HttpSignatureHeaders`, `HttpSignatureAlgorithm`,
  `HttpSignatureAlgorithmNames` and `HttpSignatureException` (every refusal is exit 43, with
  curl's message).

Every message and format is upstream's; change one only after reading upstream's source at the
targeted curl version. Held to the solution's quality gates.
