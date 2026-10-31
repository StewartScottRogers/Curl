# ADR-1145 — --dpop signs a DPoP proof for every HTTP request with a PEM key

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4162.

## Context

BL-4162 adds OAuth 2.0 Demonstrating Proof of Possession (RFC 9449, September 2023) as an extension (ADR-0999) in
`Curl.Extension.Dpop.UnitLibrary`. curl has no DPoP support. The extension is tested against RFC 9449's sections and
changes nothing without its option.

## Decision

- **Option.** `--dpop <key file>`: the PEM file (RFC 7468) of the private key proofs are signed with, as
  `EC PRIVATE KEY`, `RSA PRIVATE KEY` or PKCS #8 `PRIVATE KEY`. Its description name is `dpop`. It has no URL scheme,
  so, as ADR-1106's `--ratelimit-pace` and ADR-1135's `--record`, it adds no line to `ExtensionCatalogue`; it is
  registered in the option, description and transfer hook catalogues.
- **Algorithms.** `ES256`, `ES384` and `ES512` for P-256, P-384 and P-521 keys, `RS256` for RSA keys (RFC 7518 §3.3,
  §3.4), all in the base class library. A key on another curve, a public key alone or a file that cannot be read
  fails the transfer before anything is sent, with exit 26 (`CURLE_READ_ERROR`, the code curl gives a file it cannot
  read) and `Failed to read a DPoP private key from <file>`.
- **Proof (§4.2).** Header `typ` `dpop+jwt`, `alg`, and `jwk` holding only the public members in RFC 7638 order.
  Payload `jti` (16 random bytes, base64url), `htm` (the method curl's options send), `htu` (scheme and host in lower
  case, the port unless it is the default, and the path, without query or fragment, §4.3), `iat` from the transfer's
  `TimeProvider`, `ath` when an access token is sent, `nonce` when the origin gave one. A fresh proof goes with
  every request, the retry included.
- **Access token (§7.1).** An `--oauth2-bearer` token is sent as `Authorization: DPoP <token>`, which replaces
  curl's `Bearer` header as any `-H Authorization` does, and is hashed into `ath`. A `-H "Authorization: DPoP <token>"`
  the user gave is left as it is and its token hashed into `ath`. A `-H "Authorization: Bearer ..."` is not rewritten:
  a transfer hook adds headers and cannot take away one `-H` gave.
- **Nonces (§8, §9).** The last `DPoP-Nonce` each origin (scheme, host, port) sent is put into later proofs to it. A
  400 or 401 response that brings a nonce other than the one sent is retried once with it; the server's
  `DPoP-Nonce` is the demand, so the `use_dpop_nonce` error code in the body or `WWW-Authenticate` is not parsed.
  So that only the kept response reaches the outputs, the hook holds each try's head and body in memory until it
  decides, which means `--dpop` transfers are written when they end rather than as they arrive.
- **Scope.** HTTP and HTTPS transfers. Any other transfer runs as it would without the hook.

## Left out

- Authorization requests' `dpop_jkt` parameter (§10) and token requests: the user builds those requests; the
  proof on them is the same one this hook sends.
- Generating a key: the user supplies one (`openssl ecparam -genkey`), so the proofs stay bound to a key that
  outlives one command.
- Proofs for a proxy: RFC 9449 defines none.
