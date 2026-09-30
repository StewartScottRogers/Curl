# ADR-0233 — The hand-built TLS 1.3 client offers ECH to RFC 9849 with shared key shares and BoringSSL-style GREASE

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-706.
Builds the ECH row of ADR-0140 (`EchClientHello`); BL-711 wires it to `--ech`, BL-707
feeds it configs from HTTPS records.

## Context

`--ech` takes `false`, `grease`, `true`, `hard`, `ecl:<base64 ECHConfigList>` and
`pn:<public name>` (curl 8.23.0 manual, checked 2026-09-28). curl does ECH with
BoringSSL, wolfSSL, rustls and OpenSSL's ECH work; none of the platform builds ADR-0009
matches (Schannel, OpenSSL 3.5) can, so ADR-0140 routes every `--ech` other than `false`
to the hand-built client. RFC 9849 (TLS Encrypted Client Hello, 2026-03) fixes the wire
format and leaves the client some choices: which extensions the inner hello shares with
the outer one, the padding, how GREASE looks, and how the client ends a rejected
handshake. HPKE (RFC 9180) is `Curl.Cryptography`'s `Hpke` (BL-677).

## Decision

1. **Settings.** `Tls13ClientSettings.EncryptedClientHelloConfigs` is a decoded
   `EchConfigList` (`EchConfigList.Decode`: malformed is `decode_error`, a typed result;
   configs of another version are skipped). The offer goes to `SupportedConfig`, the first
   config with a KEM `Hpke` has (X25519 or P-256 with HKDF-SHA256), a public key HPKE can
   encapsulate to, no mandatory extension, and a suite of HKDF-SHA256 with AES-128-GCM,
   AES-256-GCM or ChaCha20-Poly1305 (the config's first such). With none, nothing is
   offered and `SendEncryptedClientHelloGrease` decides whether GREASE goes out; BL-711
   maps `hard` with no supported config to a failure before connecting. Both need
   `encrypted_client_hello` in `ExtensionOrder`, as every other built extension does.
2. **Two hellos, one set of key shares.** The inner hello (the real `server_name`, its
   own random, the `inner` extension, TLS 1.3 alone) and the outer hello (the public
   name, the connection's random, TLS 1.2 too when offered) carry the same key shares,
   suites and legacy session ID. RFC 9849 allows it (its `ech_outer_extensions` exists to
   share `key_share`), and it means the ServerHello's share works whichever hello the
   server answered, so there is one key exchange path. The inner hello is sent whole,
   without `ech_outer_extensions`: simplest, and only longer.
3. **Padding** is section 6.1.3's rule exactly: `maximum_name_length` less the name's
   length (or plus 9 with no name), then up to a multiple of 32.
4. **Acceptance.** The last 8 bytes of the ServerHello random, and the 8-byte extension
   in a HelloRetryRequest, are checked as section 7.2 says. Accepted, the transcript and
   the rest of the handshake run on the inner hello; a HelloRetryRequest that confirmed
   and a ServerHello that does not is `illegal_parameter`; after a HelloRetryRequest the
   second outer hello is sealed with the same HPKE context and an empty `enc`.
   `encrypted_client_hello` in EncryptedExtensions after acceptance is
   `unsupported_extension`, as BoringSSL and Go treat it.
5. **Rejection.** The handshake runs on the outer hello, the verifier is shown the
   public name, `retry_configs` in EncryptedExtensions are decoded (malformed is
   `decode_error`) and exposed as `EncryptedClientHelloRetryConfigs`, and once the
   server's Finished checks out the handshake fails with the new
   `TlsAlertDescription.EchRequired` (121) without sending the client's flight: RFC 9849
   section 6.1.6 has the client abort before any application data. Whether to retry with
   the configs is BL-711's.
6. **GREASE** follows BoringSSL, the ECH implementation curl is most often built with: a
   random `config_id`, HKDF-SHA256 with AES-128-GCM, a real X25519 public key as `enc`, and
   a random payload as long as a real one would be for the inner hello these settings
   build with a `maximum_name_length` of 0, plus the 16-byte tag. The hello after a
   HelloRetryRequest repeats the extension unchanged (section 6.2.1); a server's
   confirmation is ignored and its `retry_configs` are checked for syntax and dropped.
7. **Resumption inside an offer** (amended in BL-960, also decided by Claude under
   Stewart's delegation). A hello offering ECH offers `ResumptionSession` in the inner
   hello, its binder computed over the inner hello (and, after a HelloRetryRequest, the
   inner transcript). The outer hello carries a GREASE `pre_shared_key` (RFC 9849 section
   6.1.2): a random identity as long as the ticket, a random obfuscated age and a random
   binder as long as the real one, and `early_data` exactly when the inner hello has it.
   Early data is offered as without ECH and goes under the inner hello's early secret,
   the only one an accepting server can derive; a rejecting server cannot open the inner
   hello, so it drops the early data like any it cannot read. A ServerHello that accepted
   ECH is judged against the inner hello's ticket; one that rejected it answered the
   outer hello, whose `pre_shared_key` is GREASE, so selecting any identity there is
   `illegal_parameter`. GREASE hellos resume as any other.

## Alternatives considered

- **Separate key shares for the inner and outer hello.** Hides nothing more (the outer
  shares are sent in the clear either way) and doubles the key exchange state.
- **Compress the inner hello with `ech_outer_extensions`.** Shorter, but a second
  encoding path to get byte-exact for a few hundred bytes saved.
- **Fail the handshake at `Start` on an unusable config.** `Start` has no failure path;
  the settings already say what was offered (`EncryptedClientHelloOffered`), so the caller
  decides, as curl's `true` and `hard` differ exactly there.
- **Send the client Finished before `ech_required`.** RFC 9849 has the client abort once
  the server is authenticated; sending Finished gains nothing and lets the server think
  the connection is up.

## Consequences

- `Curl.Tls.UnitTests` completes accepted ECH handshakes with an in-memory client-facing
  server (`EchTestFrontEnd`) in front of `Tls13TestServer`, through a HelloRetryRequest
  too, and pins the GREASE extension for fixed randoms.
- BL-711 builds `--ech` on `EncryptedClientHelloConfigs`, `SendEncryptedClientHelloGrease`,
  `EncryptedClientHelloOffered`, `EncryptedClientHelloAccepted`,
  `EncryptedClientHelloRetryConfigs` and the `EchRequired` failure.
- BL-960 added resumption inside an ECH offer: `Curl.Tls.UnitTests` resumes through an
  accepted offer, through a HelloRetryRequest too, checks early data's secret against the
  server's, and refuses a rejecting ServerHello's `pre_shared_key`.
