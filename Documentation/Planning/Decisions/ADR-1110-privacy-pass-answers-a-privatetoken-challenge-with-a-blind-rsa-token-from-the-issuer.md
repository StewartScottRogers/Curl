# ADR-1110: --privacy-pass answers a PrivateToken challenge with a Blind RSA token from the issuer

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4138
- Decided by Claude under Stewart's delegation

## Context

Extension 37 of 63 (ADR-0999): Privacy Pass, RFC 9576 (architecture), RFC 9577 (the `PrivateToken` HTTP
authentication scheme) and RFC 9578 (issuance protocols). Upstream curl has no Privacy Pass support, so the
extension is tested against the RFCs, not against curl.

## Decision

- One option, `--privacy-pass <issuer request URL>`, in `Curl.Extension.PrivacyPass.UnitLibrary`, registered as a
  transfer hook (ADR-1007, ADR-1039) like the OAuth 2.0 extension. Without it no hook exists and every transfer is
  unchanged; with `CURL_EXTENSIONS=0` the option is not offered, so curl refuses it as an unknown option (exit 2).
- The hook runs the transfer with its outputs held back. When the answer is a 401 whose `WWW-Authenticate` carries a
  `PrivateToken` challenge (RFC 9577 §2.1.1) of token type `0x0002`, the held-back 401 is dropped, a token is issued
  and the transfer runs again with `Authorization: PrivateToken token="..."` (RFC 9577 §2.2.1). Any other first
  response is written out as received.
- Issuance follows RFC 9578 §6 (Blind RSA, 2048-bit): token input = type, a 32-byte random nonce, SHA-256 of the
  challenge, SHA-256 of the `token-key` (§6.5, `id-RSASSA-PSS` SubjectPublicKeyInfo); the `TokenRequest` (type,
  truncated key ID, blinded message) is POSTed to the issuer request URL with
  `application/private-token-request`; the 256-byte `TokenResponse` is unblinded and verified as RSASSA-PSS
  (SHA-384, 48-byte salt) before use. Blinding is RSABSSA-SHA384-PSS-Deterministic (RFC 9474 §4, §5), written by
  hand on `System.Numerics.BigInteger`; verification uses the BCL's `RSA.VerifyData`.
- The option takes the issuer's request URL directly: the client is its own attester (RFC 9576 §4.2, joint
  attester and issuer is out of the client's hands), and the URL is what the user knows.
- Issuer failures: transport errors keep their exit code, a non-2xx answer is 67 (login denied, as the OCI and OAuth
  extensions' token endpoints), a malformed key or response, or one that does not verify, is 8.
- RFC 9474's check that gcd(m, n) = 1 is not made: its failure is negligible for a 2048-bit modulus and the
  signature check after unblinding rejects any wrong result.
- The first response is buffered whole, so the extension trades streaming of that response for being able to drop
  the 401.

## Not yet done (filed as tasks, not left out)

- Token type `0x0001` (VOPRF on P-384, RFC 9578 §5) needs a hand-built P-384 and hash-to-curve.
- Discovering the request URL and keys from the issuer directory (RFC 9578 §4): done by ADR-1111, from the issuer origin given to `--privacy-pass`.
