# ADR-1117 — The Oblivious DoH extension follows RFC 9230, named by `--odoh-proxy` and `--odoh-target`

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4149.

## Context

Oblivious DoH (RFC 9230) splits a DoH lookup between a proxy, which sees the client but not the query, and a
target, which reads the query but sees only the proxy. Upstream curl has no ODoH, so this is an extension
(ADR-0999), tested against RFC 9230, never changing what curl does. Like the encrypted DNS extension (ADR-1070) it
replaces the resolver a transfer looks host names up with, through a resolver hook (ADR-1012).

## Decision

- **Names.** The extension is `oblivious-doh` in `curl --extensions`. Two options, both taking a URL and given
  together: `--odoh-proxy <URL>` and `--odoh-target <URL>`. One without the other, or a value that is not an absolute
  `http` or `https` URL, is refused with exit 2. The hook claims the resolver as `--odoh-proxy`, so it cannot be
  combined with `--doh-url`, `--dns-over-tls` or `--dns-over-quic`.
- **Specification version.** RFC 9230 (June 2022), `ObliviousDoHConfig` version `0x0001`, the only one it defines.
- **Requests.** Each query is POSTed to the proxy URL with the `targethost` and `targetpath` variables of section 4.1
  appended (the URI template `{?targethost,targetpath}` may end the proxy URL and is replaced; a `/` in the path is
  left unescaped, as the section's example shows), with media type `application/oblivious-dns-message`. One AAAA
  and one A query are sent per name, AAAA addresses first, as the DoT resolver does.
- **Encryption.** Sections 6.1 to 6.4 as written: key ID `Expand(Extract("", config), "odoh key id", Nh)`, query
  sealed with HPKE base mode and info `odoh query`, associated data `0x01 || len(key_id) || key_id`; the response key
  and nonce derived from `Export("odoh response", Nk)` with salt `Q_plain || len(resp_nonce) || resp_nonce`, the
  response nonce read from the response's `key_id` field. The pseudocode in section 6.4 names its variables
  inconsistently (`prk` and `odoh_prk`); the evident reading is taken. HPKE is `Curl.Cryptography`'s: KEMs
  DHKEM(P-256) and DHKEM(X25519), KDF HKDF-SHA256 only, AEADs AES-128-GCM, AES-256-GCM and ChaCha20-Poly1305. A
  config naming anything else is skipped, as section 5 says a client skips what it does not support.
- **Padding.** A query plaintext is padded with zeros to a multiple of 128 octets, the block length RFC 8467
  section 4.1 recommends, as section 11 asks. A response whose padding is not all zeros is refused.
- **Configs.** RFC 9230 leaves discovery of the target's configs out of scope. The client GETs them from
  `/.well-known/odohconfigs` on the target's origin, the path deployed ODoH targets serve, once per resolver, and
  uses the first usable entry.
- **HTTP.** Each request is HTTP/1.1 on its own connection with `Connection: close`, through
  `Curl.Networking`'s `TcpConnector` (TLS for `https`), the proxy's and target's own names resolved with curl's
  resolver. The response is read to the close, its body framed by chunked coding, `Content-Length` or the close,
  and limited to 1 MiB. The DNS messages are `Curl.Networking`'s public `DnsQueryEncoder` and `DnsAnswerDecoder`.
- **Failure.** Any failure (configs not served or unusable, a connection that fails, a status other than 200, a
  response that does not open) gives no addresses, which the connector reports as curl's exit 6.

## Left out

- Discovering configs from the target's DNS HTTPS record (`odohconfig` SvcParam): it needs a lookup the resolver
  is replacing; the well-known path serves the same configs.
- HTTP/2 and connection reuse: one lookup is two small requests, and HTTP/1.1 keeps the extension free of
  `Curl.Protocol.Http`, which an extension may not reference.
- KDFs other than HKDF-SHA256: `Curl.Cryptography`'s HPKE implements only that one.

## Consequences

`Curl.Extension.ObliviousDoh.UnitLibrary` registers one line each in the option, description and resolver hook
catalogues; it adds no URL scheme, so `ExtensionCatalogue.cs` gets no line. Without the options every transfer
resolves exactly as before.
