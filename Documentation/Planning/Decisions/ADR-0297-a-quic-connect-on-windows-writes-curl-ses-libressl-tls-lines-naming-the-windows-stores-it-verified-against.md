# ADR-0297 — A QUIC connect on Windows writes curl.se's LibreSSL TLS lines, naming the Windows stores it verified against

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1050.
Carries out ADR-0144's Output consequence for Windows.

## Context

ADR-0144 decided that `-v` for a QUIC connect prints, on Windows, the TLS lines of curl.se's
build (curl 8.18.0, LibreSSL 4.2.1, ngtcp2 1.21.0), the only Windows curl that speaks HTTP/3,
with `SSL Trust Anchors:` naming the anchors Curl actually used. Curl printed none: the
writers word TLS events as `PlatformTlsBackend.ForProcess`, Schannel on Windows, which
prints only ALPN lines, and QUIC offers none.

Measured 2026-10-01 with `Record-CurlExchange.ps1 -NoServer` and curl.se's build,
`-s -v -o NUL --http3-only https://cloudflare-quic.com/` (BL-1050's Notes):

- no CA option: `SSL Trust Anchors:` / `  CA Blob from configuration` (its embedded bundle);
- `--ca-native`: `  Native: Windows System Stores ROOT+CA`, then the blob line;
- `--cacert <file>`: `  CAfile: <file>`;
- `-k`: `SSL Trust: peer verification disabled`, and the handshake ends with
  ` SSL certificate verification failed, continuing anyway!`;
- the handshake lines are OpenSSL's but with `[blank] / UNDEF` for group and signature,
  `Public key type ? (<bits>/<secbits> Bits/secBits)` and no `OpenSSL verify result` line.

## Decision

1. `TlsHandshakeEvent` and `TlsTrustEvent` gain `IsQuic`; `QuicDialer` sets it on both.
   `TlsTrustEvent` gains `UsesWindowsSystemStores`, worded `  Native: Windows System Stores
   ROOT+CA` before any blob, file or directory line.
2. Under the Schannel wording a QUIC trust event gets the OpenSSL `SSL Trust` lines and a
   QUIC handshake event gets the LibreSSL lines above (`OpenSslHandshakeText.LibreSslLines`).
   A TCP event under Schannel is unchanged; under OpenSSL wording (Linux, macOS) a QUIC
   event gets exactly the TCP path's lines.
3. On Windows without `--cacert`, Curl verifies against the Windows stores, so the trust
   line is `Native: Windows System Stores ROOT+CA`, the line curl.se's build writes for those
   stores; its `CA Blob from configuration` line is not written, because Curl embeds no
   bundle (as ADR-0144 leaves out the `Note: Using embedded CA bundle` lines). With
   `--cacert` the `CAfile:` line names the file, as measured.

## Consequences

- On Windows `curl -v --http3-only https://<host>/` writes, between `Trying` and
  `Established connection`, the measured lines with one deliberate difference: the store
  line instead of the blob line when no `--cacert` is given.
- The `-k` failure line is the OpenSSL build's; it matched the measurement. A failed
  verification without `-k` ends the transfer before these lines and is not changed here.
- LibreSSL's key sizes for Ed25519 and Ed448 keys were not measured; they are taken as
  OpenSSL's.
