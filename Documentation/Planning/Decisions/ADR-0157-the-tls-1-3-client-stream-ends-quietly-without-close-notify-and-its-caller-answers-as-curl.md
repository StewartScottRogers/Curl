# ADR-0157 — The TLS 1.3 client stream ends quietly without close_notify, and its caller answers as curl

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-700.

## Context

BL-700 runs the hand-built TLS 1.3 client (ADR-0140, handshake in ADR-0146) over a byte
stream: `Tls13ClientConnection` runs the handshake over the caller's `Stream`, and
`Tls13ClientStream` is what the rest of Curl reads and writes afterwards, as it does the
stream `SslStream` gives. RFC 8446 section 5 leaves several choices to the client, and
curl's builds have made them:

1. **A transport that ends without `close_notify`.** RFC 8446 section 6.1 calls it a
   truncation the application may detect. curl fails the transfer when a read needs more
   bytes and none come: the OpenSSL build reports `OpenSSL SSL_read: error:0A000126:SSL
   routines::unexpected eof while reading` and the Schannel build `schannel: server closed
   abruptly (missing close_notify)`, both exit 56 (`Curl.Networking.UnitLibrary`'s
   `TlsFailureMessages`, ADR-0096). A transfer whose length is known never reads past its
   end, so it never sees the missing alert.
2. **Where a received alert, a corrupted record and a closed transport surface** - during
   the handshake and after it.
3. **Record details:** padding, the `legacy_record_version` of the first ClientHello, the
   middlebox compatibility `change_cipher_spec`, and KeyUpdate.
4. **Which suites the record layer protects.** AES-CCM is not in the BCL on every CI
   platform (ADR-0118) and is hand-built in BL-738, which has not landed.

## Decision

1. **`Tls13ClientStream` ends quietly, and says how it ended.** A read returns 0 once the
   server's `close_notify` arrives or the transport ends, at a record boundary or inside a
   record, exactly as `SslStream` does. `CloseNotifyReceived` tells the two apart. The
   TLS library does not know whether the caller needed more bytes, so the caller does the
   mapping: where a read that returns 0 without `close_notify` leaves a transfer
   unfinished, it fails with exit 56 and the text of the build it stands in for (the
   OpenSSL text, since ADR-0140 runs this client where the OpenSSL or LibreSSL build is
   matched). BL-708 wires that when it offers this client through `ITlsProvider`.
2. **Failures.** A handshake failure is a `TlsHandshakeFailure` whose `Origin` says whether
   the client sent the alert, the server sent it, or the transport closed first; the
   caller maps it to exit 35 or 60 as before. After the handshake, a read or write fails
   with a `TlsAlertException` (as `SslStream` throws an `IOException`), which carries the
   alert and whether the server sent it. An alert the client raises - `bad_record_mac` for
   a record that fails authentication or is shorter than a tag, `record_overflow`,
   `unexpected_message`, `decode_error`, `illegal_parameter` - is sent first; a transport
   that fails while the alert goes out is ignored. A server's fatal alert is not answered.
3. **Records.** Records are never padded (none of curl's builds pads); padding a server
   sends is removed. The first ClientHello's record carries `legacy_record_version`
   0x0301, as OpenSSL and Schannel send (`Tls13ClientSettings.ClientHelloRecordVersion`);
   every other record carries 0x0303. In middlebox compatibility mode the client sends one
   `change_cipher_spec` before its second flight (or before a second ClientHello after a
   HelloRetryRequest), and ignores the server's single-byte `change_cipher_spec` until
   the handshake completes; any other one is `unexpected_message`. A server KeyUpdate
   moves the read keys on; when it requests an update, the client answers with its own
   KeyUpdate before its write keys move on, with no application write in between.
   `UpdateKeysAsync` lets the caller start one. `ShutdownAsync` sends `close_notify` once
   and leaves reads open; disposing does not send it.
4. **Suites.** `Tls13RecordProtection` protects the AES-GCM and ChaCha20-Poly1305 suites.
   A connection offering a CCM suite is refused with `ArgumentException` before anything
   is sent, until BL-811 adds AES-CCM and AES-CCM8 on BL-738's hand-built AEAD. The AEADs
   are renamed from `ITls12Aead` and its `*Tls12Aead` implementations to `ITlsAead` and
   `*TlsAead`, since TLS 1.2 and 1.3 share them.

## Consequences

- The stream behaves as `SslStream` does, so BL-708 can put it behind the same seam
  without the protocols noticing.
- Curl's exit 56 for a truncated close-delimited transfer depends on BL-708 reading
  `CloseNotifyReceived`; until then the flag is only exercised by `Curl.Tls.UnitTests`.
- The synchronous `Read` and `Write` throw `NotSupportedException`: Curl is async all the
  way (root `CLAUDE.md`).

## Alternatives considered

- **Throw from the read when the transport ends without `close_notify`.** Rejected: it
  would fail transfers that curl completes (a known length ends before the missing alert
  matters), and `SslStream`, which Curl already sits on, returns 0.
- **Pad records to hide lengths.** Rejected: no curl build does, and a server would see a
  different client.
