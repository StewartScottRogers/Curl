# ADR-0969: The hand-built TLS client's handshake message observer sits on the connections

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4014).

## Context

OpenSSL's `-v` trace (curl's `ossl_trace` message callback) prints every handshake message
and `change_cipher_spec` a TLS connection sends and receives, the TLS 1.3 ones after
ServerHello as decrypted. `SslStream` hides those, so only the hand-built client in
`Curl.Tls.UnitLibrary` can feed such a trace (BL-4009, split into BL-4012 and BL-4014). The
handshake classes (`Tls13ClientHandshake`, `Tls12ClientHandshake`) are I/O free: they frame the
messages they receive but never see a record header, and they do not know when the TLS 1.3
middlebox-compatibility `change_cipher_spec` goes out; the connections write and read the
records.

## Decision

- `TlsHandshakeMessageObserver` is a delegate `(TlsTrafficDirection direction, TlsContentType
  contentType, ushort recordVersion, ReadOnlySpan<byte> message)`, set through
  `Tls13ClientSettings.HandshakeMessageObserver` and `Tls12ClientSettings.HandshakeMessageObserver`.
  `TlsClientConnection` needs no change: it passes each version's settings through.
- The connections (`Tls13ClientConnection`, `Tls12ClientConnection`) call it, because only they
  know the wire order of `change_cipher_spec` against the handshake messages and the record
  version: each sent handshake message and `change_cipher_spec` as it is written, with the
  version the record header carries; each received `change_cipher_spec` as it arrives.
- Received handshake messages are framed by the handshake classes, so each hands every whole
  message, header included, to an internal `MessageReceived` hook before acting on it; the
  connection sets the hook to call the observer with the version of the record that completed
  the message. `Tls12RecordContent` carries the record header's version for this.
- `Tls13ClientHandshake` used on its own, as QUIC does, never calls the observer: QUIC's
  handshake has no records.
- With no observer set, nothing is reported and nothing on the wire changes.

## Consequences

- A `-v` trace from the hand-built client can print every message in wire order, the TLS 1.3
  encrypted flight included.
- A message received across several records is reported once, with the last record's version.
