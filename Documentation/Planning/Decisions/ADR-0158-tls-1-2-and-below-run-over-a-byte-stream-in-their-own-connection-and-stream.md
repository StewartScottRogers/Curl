# ADR-0158 — TLS 1.2 and below run over a byte stream in their own connection and stream

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-815.

## Context

ADR-0140's "Connection" row names one `TlsClientConnection` that sends the hello, picks
the TLS 1.3 or 1.2 path from the ServerHello and returns one `TlsClientStream`. BL-700
built the TLS 1.3 half as `Tls13ClientConnection` and `Tls13ClientStream` (ADR-0157).
BL-815 has to drive `Tls12ClientHandshake` (BL-703, ADR-0154) over a byte stream the same
way, so BL-708's hand-built `ITlsProvider` can run every version the routing rule sends
it. The two handshakes take different settings records (`Tls13ClientSettings`,
`Tls12ClientSettings`), send different ClientHellos, and have different record layers.

Three record-layer details are left to the client:

1. The version of the records it writes before the version is known, and what it checks
   on the records it reads.
2. What a HelloRequest after the handshake does, since Curl never renegotiates.
3. Whether disposing the stream sends `close_notify`.

## Decision

1. **A separate `Tls12ClientConnection`, `Tls12ClientStream` and `Tls12ConnectResult`**,
   shaped exactly as their TLS 1.3 counterparts: `ConnectAsync(Stream, Tls12ClientSettings,
   ITlsRandomSource, IServerCertificateVerifier, CancellationToken)` returns the stream or
   a `TlsHandshakeFailure` with its `Origin`, and throws only cancellation, argument
   errors and the transport's own failures. The version-picking `TlsClientConnection` of
   ADR-0140 is left to the task that offers one ClientHello spanning TLS 1.3 and 1.2
   (the TLS 1.2 handshake cannot yet continue from a TLS 1.3 ClientHello); until then the
   caller picks the connection from the version range it was given.
2. **Record versions.** The ClientHello record carries TLS 1.0 (0x0301), as OpenSSL's
   does; once the ServerHello names the version, every record written carries it and
   every record read must carry it, or the client sends `protocol_version`. Before the
   ServerHello a record of any major version other than 3 is `protocol_version` too
   (OpenSSL's "wrong version number").
3. **HelloRequest is ignored** after the handshake, whole or split across records, as the
   I/O-free handshake already ignores it during one; a HelloRequest with a body is
   `decode_error`, and any other handshake message is `unexpected_message`.
4. **Closing as ADR-0157 decides for TLS 1.3.** `ShutdownAsync` sends `close_notify` once
   and leaves reads open; disposing does not send it (as `SslStream`'s disposal does
   not). A read returns 0 at the server's `close_notify` or at a bare transport end, and
   `CloseNotifyReceived` tells them apart. Any other alert, warning or fatal, fails the
   stream with `TlsAlertException`.
5. **The BEAST split is a setting.** `Tls12ClientSettings.InsertEmptyFragment` (on by
   default) is passed to `Tls12RecordWriteState`; `--ssl-allow-beast` turns it off.

## Consequences

- BL-708 can put both connections behind its `ITlsProvider` with the same result handling:
  a failure's `Origin` and `CertificateRejection`, and the stream's `CloseNotifyReceived`.
- Two small stream classes repeat the `Stream` boilerplate; merging them is left until the
  version-picking connection exists and knows what one stream must expose.

## Alternatives considered

- **Build ADR-0140's single `TlsClientConnection` now.** Rejected: it needs one ClientHello
  offering both TLS 1.3 and 1.2 and a TLS 1.2 handshake that continues from it, which
  neither handshake supports yet; it would widen this task past its `touches`.
- **Send `close_notify` on dispose.** Rejected: the TLS 1.3 stream does not, `SslStream`
  does not, and a caller that wants it calls `ShutdownAsync`.
- **Answer HelloRequest with a `no_renegotiation` warning.** Rejected: the handshake
  already ignores it, and a warning the server may treat as fatal gains Curl nothing.
