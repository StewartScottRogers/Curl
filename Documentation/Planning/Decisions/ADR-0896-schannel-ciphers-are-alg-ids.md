# ADR-0896: The Schannel build reads `--ciphers` as ALG_IDs

- Status: Accepted
- Date: 2026-10-26
- Decided by Claude under Stewart's delegation (BL-3551, CGR-022). Amends ADR-0011.

## Context

ADR-0011 had the Schannel build refuse every `--ciphers` value with exit 59. curl 8.22.0's
Schannel build (`set_ssl_ciphers`, `get_alg_id_by_name`) reads the list as Windows ALG_IDs,
so `--ciphers CALG_AES_128` worked with curl and failed with Curl (CGR-022).

## Decision

`SchannelAlgorithmList` applies the list in both providers, as measured with curl 8.22.0
Schannel on Windows 11 against `openssl s_server`:

- Entries are split on `:`; an empty entry after the last colon is not read, and reading
  stops after 47 algorithms (curl's `NUM_CIPHERS`).
- An entry is a non-zero decimal ALG_ID (leading digits, an `int`), or a `CALG_*` name matched
  exactly from curl's table (without `CALG_NULLCIPHER` and the `CALG_THIRDPARTY_*` names,
  which the measured build refuses), or starts with `USE_STRONG_CRYPTO` or
  `SCH_USE_STRONG_CRYPTO`. Anything else is exit 59 `schannel: Failed setting algorithm
  cipher list`.
- A list naming algorithms none of which AcquireCredentialsHandle accepted in the measurement
  (`CALG_SHA`/`SHA1`, `RSA_SIGN`, `RSA_KEYX`, `AES_128`, `AES_256`, `SHA_256`, `SHA_384`,
  `ECDH_EPHEM`, `ECDSA`) is exit 35 `schannel: AcquireCredentialsHandle failed:
  SEC_E_ALGORITHM_MISMATCH (0x80090331) - ...` before connecting.
- A list takes curl onto Schannel's legacy credential, which offers no TLS 1.3, so a range
  reaching TLS 1.3 from below stops at TLS 1.2 (a TLS 1.3-only server is then curl's
  `SEC_E_UNSUPPORTED_FUNCTION` exit 35). `SslStreamTlsProvider` keeps the options it is given
  and applies the ceiling to the protocols it offers.

The accepted algorithms do not narrow the suites offered: every measured accepted list
succeeded against a default server, and modelling Schannel's per-algorithm suite filter is
left until a case needs it. `CALG_ECDSA` alone against an RSA server is a handshake failure in
curl and a success here, for the same reason.

## Consequences

`--ciphers` and `--proxy-ciphers` with ALG_ID names behave as in curl on Windows; the OpenSSL
build is unchanged.
