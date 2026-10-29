# ADR-0983: The hand-built client's OpenSSL build reports every handshake message, its group and the peer signature type

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (BL-4009)

## Context

curl's OpenSSL build writes one `-v` line per handshake message and `change_cipher_spec`
through OpenSSL's message callback (`TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):`),
and names the negotiated group and the server's signature type on its `SSL connection using`
line. `SslStream` hides those messages, so only `HandBuiltTlsProvider` can report them. ADR-0971
(BL-4014) gave `Curl.Tls` a `TlsHandshakeMessageObserver`, and BL-4013 exposed the peer
signature scheme and key algorithm.

## Decision

- In the OpenSSL build `HandBuiltTlsProvider` hands each handshake an observer,
  `HandBuiltHandshakeMessageTrace`, which reports one `TlsMessageEvent` per message, whole with
  its header, in wire order. The Schannel build reports none (its ticket lines stay ADR-0309's).
- The version on each event is OpenSSL's callback version, `s->version`, not the record's: the
  ClientHello's highest offered version for what the client sends before the server answers,
  the negotiated version from the server's first message on (so a TLS 1.3 and 1.2 offer
  answered with TLS 1.2 prints `TLSv1.3 (OUT), ... Client hello` then `TLSv1.2 (IN)`). That
  version is known only once the handshake ends, so the trace holds the messages until then
  and reports them before the verify result; a handshake that failed uses what the verifier
  saw negotiated, else the offered version. Later messages (TLS 1.3 `NewSessionTicket`) are
  reported as they arrive.
- `TlsHandshakeEvent.NegotiatedGroupName` is `SSL_get0_group_name`'s name for the group and
  `PeerSignatureTypeName` is `OBJ_nid2sn` of the signature type NID: `RSASSA-PSS` for an
  RSA-PSS scheme, else the server key's algorithm (`rsaEncryption`, `id-ecPublicKey`,
  `ED25519`, `ED448`), in `OpenSslHandshakeNames`. Both builds set them; only the OpenSSL
  build's text prints them.
- Not measured against a live curl in this run: no OpenSSL build of curl was at hand on the
  lane's Windows host. The version rule follows OpenSSL's `ssl3_do_write` and
  `tls_get_message_body`, which pass `s->version` to the callback, and the wording is
  `OpenSslMessageText`'s, already measured.

## Consequences

- `-v` of a hand-built OpenSSL-build handshake prints OpenSSL's message trace.
- The held messages print after anything the handshake reports while it runs.
