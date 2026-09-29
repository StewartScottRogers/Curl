# ADR-0154 — The TLS 1.2 client handshake refuses legacy renegotiation, ignores HelloRequest and matches OpenSSL's alerts

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-703.

## Context

BL-703 builds `Tls12ClientHandshake` in `Curl.Tls.UnitLibrary`: the TLS 1.2, 1.1 and 1.0
client handshake of the hand-built TLS client (ADR-0140), on top of BL-702's PRF and
record layer (ADR-0150). ADR-0140 fixes what it supports (the key exchanges, suites,
groups, signature algorithms and resumption in "What the client supports") and that it
runs over TCP only where `SslStream` cannot, which means it stands in for curl.se's
LibreSSL build on Windows and the OpenSSL build elsewhere. RFC 5246 and its extensions
leave several choices open, and where OpenSSL has made one, the hand-built client must
make the same so a server sees the same client.

1. **How the handshake meets its caller.** The TLS 1.3 handshake (ADR-0146) is a
   message-level state machine that hands out secrets per encryption level. TLS 1.2 has
   no encryption levels: the ChangeCipherSpec record switches keys, once each way.
2. **A server without secure renegotiation** (no `renegotiation_info` in its
   ServerHello, RFC 5746 section 3.4). The RFC lets the client continue or abort.
   OpenSSL 3 aborts with `handshake_failure` ("unsafe legacy renegotiation disabled")
   unless `SSL_OP_LEGACY_SERVER_CONNECT` is set, and curl does not set it.
3. **HelloRequest.** A server may ask for renegotiation at any time. curl never
   renegotiates.
4. **Small DHE groups.** The server chooses the prime. OpenSSL 3 at its default security
   level refuses a group under 1024 bits with `handshake_failure` ("dh key too small").
5. **Resumption by ticket.** RFC 5077 section 3.4 lets the client send a session ID with
   its ticket so it can tell whether the server resumed.
6. **CertificateStatus.** RFC 6066 lets a server that echoed `status_request` still omit
   the CertificateStatus; the verifier (ADR-0140, "The verification hand-off") must see
   the stapled response with the chain.
7. **TLS 1.0 and 1.1 RSA signatures.** They are PKCS #1 block type 1 over MD5 and SHA-1
   with no DigestInfo (RFC 2246 section 7.4.3). The BCL's `RSA` signs and verifies only
   with a DigestInfo.
8. **Alerts.** Where RFC 5246 names no alert, OpenSSL's client code does.

## Decision

1. `Tls12ClientHandshake` is I/O-free like its TLS 1.3 sibling: `Start()` returns the
   ClientHello, `ReceiveHandshake(bytes)` takes handshake record content, and
   `ReceiveChangeCipherSpec(content)` takes the server's ChangeCipherSpec. Each returns
   `Tls12OutgoingMessage`s to send in order, handshake messages and the client's own
   ChangeCipherSpec among them. The handshake exposes `RecordProtection` and `KeyBlock`
   once known; the caller switches its write state to the client keys after sending the
   ChangeCipherSpec, and its read state to the server keys after
   `ReceiveChangeCipherSpec` accepts the server's. A ChangeCipherSpec anywhere else, or
   while a handshake message is partly received, is `unexpected_message`; any content
   but the byte 1 is `decode_error`.
2. The client sends an empty `renegotiation_info` extension (and may list
   `TLS_EMPTY_RENEGOTIATION_INFO_SCSV` among its suites), and refuses a ServerHello
   without the extension, or with a non-empty one, with `handshake_failure`, as OpenSSL
   does.
3. A HelloRequest is ignored wherever it arrives, during or after the handshake, and
   stays out of the transcript (RFC 5246 section 7.4.1.1); one with a body is
   `decode_error`. Curl never renegotiates, so nothing else follows from it.
4. A DHE prime shorter than 128 bytes (1024 bits) is `handshake_failure`. A prime and
   generator that are no group, or a public value outside 1 < y < p - 1, is
   `illegal_parameter`. The pre-master secret is the shared secret with its leading zero
   bytes stripped (RFC 5246 section 8.1.2).
5. A session with a ticket is offered with a fresh random 32-byte session ID, as OpenSSL
   does; a session without one offers its session ID. The server's echo of that ID is
   what marks the handshake resumed. A resumption must keep the session's version and
   suite (`illegal_parameter` otherwise) and its extended master secret
   (`handshake_failure` otherwise, RFC 7627 section 5.3). A resumed session keeps its
   ticket unless the server issues a new one.
6. The server's chain goes to `IServerCertificateVerifier` when the first message after
   any CertificateStatus arrives, with the stapled response if there was one. A server
   that echoed `status_request` may omit the CertificateStatus.
7. A TLS 1.0 or 1.1 RSA signature is checked with the public operation s^e mod n over
   `BigInteger`, which involves no secret. Signing one needs the private operation,
   which the BCL cannot do without a DigestInfo and which must be constant time, so an
   RSA client key cannot yet sign a TLS 1.0 or 1.1 CertificateVerify: the client answers
   such a CertificateRequest with an empty Certificate, as RFC 5246 section 7.4.6 allows,
   until the follow-up task builds the private operation. ECDSA client keys sign with
   SHA-1 there.
8. Alerts follow OpenSSL's client: a version outside the range `protocol_version`; the
   RFC 8446 downgrade sentinel in a TLS 1.1 or 1.0 ServerHello to a client offering TLS
   1.2 `illegal_parameter`; a suite not offered, one TLS 1.2 alone has at an older
   version, or a compression method other than null `illegal_parameter`; an extension
   not offered `unsupported_extension`; a server key of the wrong type for the suite
   `handshake_failure` (`ssl3_check_cert_and_algorithm`); an empty server Certificate
   `decode_error`, as in the TLS 1.3 path; a ServerKeyExchange signed with a scheme not
   offered, or an explicit curve, `illegal_parameter`; a wrong signature or Finished
   `decrypt_error`; a message out of order `unexpected_message`.

The suite table holds every ECDHE, DHE, RSA and anonymous suite whose bulk cipher the
record layer protects today; the CCM, CCM8 and RC4 suites, the DHE-DSS suites and the
x448 and brainpool groups are follow-up tasks that wait on their primitives.

## Consequences

- A server that does not support secure renegotiation fails the hand-built path exactly
  as it fails curl's OpenSSL build.
- The caller owns the record layer: BL-708's connection wires `Tls12OutgoingMessage`,
  `KeyBlock` and `ReceiveChangeCipherSpec` to `Tls12RecordWriteState` and
  `Tls12RecordReadState`.
- `Tls12Session` carries what `TlsSessionCodec` (BL-701) needs to write OpenSSL's
  `SSL_SESSION` for a TLS 1.2 session.
- Until the follow-up lands, a TLS 1.0 or 1.1 server that insists on a client
  certificate fails an RSA-keyed client where OpenSSL's would succeed.

## Alternatives considered

- **Hand the handshake the record layer**, so it returns protected records. It would
  tie the handshake to TCP framing and duplicate what BL-702's read and write states
  already do; returning the key block keeps the handshake testable message by message,
  as the TLS 1.3 one is.
- **Continue without `renegotiation_info`**, as RFC 5746 permits. OpenSSL's curl
  refuses, so a server would see two different clients depending on the path taken.
- **Answer HelloRequest with a `no_renegotiation` warning alert.** OpenSSL renegotiates
  instead, and curl never asks it not to; ignoring the request is what RFC 5246 allows
  a client that will not renegotiate, and adds no alert path the record layer must carry.
- **Sign TLS 1.0 and 1.1 RSA with `BigInteger.ModPow`.** It is not constant time, so it
  would leak the client's private key through timing; the private operation belongs in
  `Curl.Cryptography.UnitLibrary`, built for that.
