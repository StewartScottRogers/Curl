# ADR-0222 — The hand-built TCP client sends the platform profile's hello, cut to what it can honour

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-820.
Carries out ADR-0140's "Default ClientHello" for `HandBuiltTlsProvider` (ADR-0162
decision 4), which until now sent `Curl.Tls`'s default `Tls13ClientSettings` and
`Tls12ClientSettings` lists.

## Context

ADR-0140 says a server should see the platform curl's ClientHello whichever TLS client
runs. BL-787 made the measured hellos data (`ClientHelloProfile.Schannel`,
`ClientHelloProfile.OpenSsl`); BL-821, BL-786, BL-879 and BL-880 made the TLS client able to
run what they offer (TLS 1.3 and 1.2 in one hello, certificate compression,
X25519MLKEM768 and x448 key shares, post-handshake authentication). Three questions were
left for the mapping:

1. The OpenSSL profile offers signature schemes the client cannot check: ML-DSA
   (`0x0904`-`0x0906`), Ed448 (`0x0808`), the TLS 1.3 brainpool schemes
   (`0x081a`-`0x081c`) and the SHA-224 schemes (`0x0301`, `0x0303`). A server that picks
   one would fail a handshake real curl completes.
2. The OpenSSL profile has no `status_request`, but `--cert-status` needs one.
3. A range whose ceiling is below TLS 1.3 runs `Tls12ClientConnection`, whose hello has a
   fixed OpenSSL extension order and no setting to change it.

## Decision

`HandBuiltTlsProvider` takes `ClientHelloProfile.Schannel` when it matches the Schannel
build and `ClientHelloProfile.OpenSsl` when it matches the OpenSSL build, and
`ClientHelloProfileMapping` turns it into the TLS settings: the profile's extension order;
its `renegotiation_info`, `ec_point_formats`, `session_ticket`, `encrypt_then_mac`,
`extended_master_secret`, `status_request` and `psk_key_exchange_modes` sent verbatim as
fixed extensions; its suites, groups, key share groups and compression algorithms; and a
32-byte legacy session ID. Options change only the lists: `--ciphers`/`--tls13-ciphers`
the suites, the version range `supported_versions` and which suites are offered,
`--no-alpn` removes ALPN, and the connection's protocols fill ALPN.

1. **Signature schemes the client cannot check are left out.** The hello offers the
   profile's schemes that `TlsSignatureScheme.IsCertificateVerifyScheme` or `IsTls12Scheme`
   accepts, in the profile's order. The Schannel list loses nothing; the OpenSSL list loses
   the nine above. Advertising a capability the client lacks trades a byte-exact hello for
   handshakes that fail where curl's succeed, which is worse for a drop-in replacement.
   Once the client checks a scheme, the filter lets it through with no change here.
2. **`--cert-status` adds `status_request` after `supported_groups`**, where OpenSSL's
   extension table places it; a profile that already sends it (Schannel's) keeps its order.
3. **Below a TLS 1.3 ceiling the profile's lists apply, not its order.** The TLS 1.2
   hello takes the profile's TLS 1.2 suites, its ECDHE groups, its TLS 1.2 signature
   schemes, and sends `session_ticket`, `encrypt_then_mac` and `extended_master_secret`
   only when the profile does; the extension order stays `Tls12ClientHelloBuilder`'s
   (OpenSSL's). Taking the profile's order there needs a `Curl.Tls` change, filed as
   follow-up work.

## Consequences

- `HandBuiltTlsProviderTests.ClientHello` captures the first record each build sends and
  rebuilds it from the profile byte for byte.
- A server now sees X25519MLKEM768 (OpenSSL) or three key shares (Schannel) from the
  hand-built client, as it does from curl.
