# ADR-0229 — TLS-SRP runs RFC 5054 in the hand-built TLS 1.2 client as OpenSSL does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-704.
Builds the SRP row of ADR-0140; BL-712 wires it to `--tlsuser`, `--tlspassword` and
`--tlsauthtype`.

## Context

curl offers TLS-SRP only in its OpenSSL and GnuTLS builds, so ADR-0140 routes it to the
hand-built client on every platform. RFC 5054 defines the `srp` extension (12), the SRP
ServerKeyExchange (N, g, s, B), the client's A in its ClientKeyExchange, SRP-6a's
arithmetic with SHA-1, the groups of Appendix A and nine suites (section 2.7). RFC 5054
leaves some choices open; OpenSSL 3.5's `ssl/tls_srp.c`, `ssl/statem/extensions_clnt.c`
and `crypto/srp/srp_lib.c` make them, and curl's OpenSSL build is the one being matched.

## Decision

1. **The nine suites.** `Tls12CipherSuite` gains `TLS_SRP_SHA_*`, `TLS_SRP_SHA_RSA_*` and
   `TLS_SRP_SHA_DSS_*`, each with AES-256-CBC, AES-128-CBC and 3DES-EDE-CBC and HMAC-SHA1 -
   every SRP suite OpenSSL 3.5.5's `ALL:COMPLEMENTOFALL` lists. `Tls12KeyExchange.Srp` is
   their key exchange; plain SRP uses `Tls12Authentication.Anonymous` (no Certificate, an
   unsigned ServerKeyExchange), the RSA and DSS forms their certificate's signature.
2. **Offered only with credentials.** `Tls12ClientSettings.SrpCredentials`
   (`TlsSrpCredentials`: user name and password) turns SRP on. Without it the hello
   leaves the SRP suites out, as OpenSSL disables `SSL_kSRP` with no SRP login, and a
   ServerHello choosing a suite the hello did not carry is `illegal_parameter`. With it
   the ClientHello carries `srp` right after `server_name` (OpenSSL's extension order),
   also in the combined TLS 1.3 and 1.2 hello (ADR-0205).
3. **Strings as UTF-8, no SASLprep.** RFC 5054 section 2.3 asks for SASLprep; OpenSSL
   takes the bytes it is given and curl passes its arguments through, so Curl uses the
   UTF-8 bytes of the user name and password unchanged.
4. **Groups: Appendix A only.** `SrpGroup` holds the seven groups. A ServerKeyExchange
   whose N and g are not one of them is `insufficient_security`, OpenSSL's
   `SRP_check_known_gN_param` answer.
5. **B is checked before use.** B of 0 or of N or more is `illegal_parameter`: RFC 5054
   section 2.5.4 mandates it for B % N = 0 and OpenSSL's `srp_verify_server_param`
   refuses any B not below N.
6. **The private value a is 48 random bytes**, OpenSSL's `SSL_MAX_MASTER_KEY_LENGTH`, drawn
   from `ITlsRandomSource`.
7. **The premaster secret is S without leading zero bytes**, OpenSSL's `BN_bn2bin`; `PAD()`
   applies only where RFC 5054 writes it (k and u).
8. **A wrong password** leaves client and server with different master secrets, so the
   server's Finished fails with `decrypt_error` (or the server ends it first with its own
   alert). curl's messages and exit codes for it are BL-712's to measure and pin.

`SrpClient` holds the arithmetic as pure public functions (k, x, v, A, u, S) over
`System.Numerics.BigInteger` and `SHA1`, and `Curl.Tls.UnitTests` reproduce every value
of RFC 5054 Appendix B with it.

## Consequences

- The hand-built client can authenticate with SRP in TLS 1.0, 1.1 and 1.2 in every
  Appendix A group; TLS 1.3 has no SRP, so a server that picks TLS 1.3 never runs it.
- BL-712 has only to build `TlsSrpCredentials` from the options and route the transfer.
