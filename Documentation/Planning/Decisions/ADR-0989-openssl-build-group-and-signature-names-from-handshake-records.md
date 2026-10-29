# ADR-0989: The OpenSSL build names the TLS group and peer signature type from the plaintext handshake records

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4041)

## Context

curl's OpenSSL build prints `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS`
for `-v`: the group from `SSL_get0_group_name` and the signature type from
`OBJ_nid2sn(SSL_get_peer_signature_type_nid)`. `SslStream` exposes neither, so
`SslStreamTlsProvider` left `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName`
null and Curl printed `[blank] / UNDEF` on Linux and macOS (CGR-226, BL-3755).

## Decision

- In the OpenSSL build `SslStreamTlsProvider` gives the `ConnectionStream` under the `SslStream`
  a `ServerHandshakeRecordReader` (BL-4039, BL-4040) as `HandshakeRecords`; every byte
  `ConnectionStream.ReadAsync` reads is shown to it, beside `TicketRecords`, and the reader stops
  itself at the first ChangeCipherSpec or application data record (or 64 KiB).
- After the handshake `DescribeHandshake` sets `NegotiatedGroupName` from `GroupName` and
  `PeerSignatureTypeName` from `PeerSignatureTypeName`, passing the server certificate's key
  algorithm OID (`SslStreamTlsProvider.KeyAlgorithmOf`), since TLS 1.3's CertificateVerify is
  encrypted and RSA keys always sign with RSA-PSS there. TLS 1.2's names come from the
  ServerKeyExchange.
- The Schannel build gets no reader and reports neither name: curl's Schannel build prints no
  group or signature type.
- A failed handshake's event keeps no names; the reader only feeds a completed handshake.

## Consequences

Reading the plaintext records costs one copy of at most the server's first flight per handshake.
A server whose answer the reader cannot parse leaves the names null, as before.
