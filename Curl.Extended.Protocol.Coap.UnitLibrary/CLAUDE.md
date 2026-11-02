# Curl.Extended.Protocol.Coap.UnitLibrary

The CoAP extension (ADR-0999), tested against RFC 7252, RFC 7959, RFC 7641 and RFC 6347, not
against curl. It references only `Curl.Extension.Abstractions` (and through it
`Curl.Protocol.Abstractions`), the shared layer `Curl.Tls` (for `TlsContentType`, `TlsPrf`, `Tls12KeyBlock` and
`HandshakeType`, ADR-1042) and the base class library. `Client/` holds the `coap://` handler,
registered by one line in `ExtensionCatalogue.cs` and described in `ExtensionDescriptionCatalogue.cs`.

`Client/` (namespace `Curl.Extended.Protocol.Coap.Client`, ADR-1047) makes requests over UDP:

- `CoapExtension` is the description `curl --extended-help` lists and the handler factory, over
  `ProtocolHandlerServices.DatagramConnector` and `ProtocolHandlerServices.TlsProvider`, for `coap` and `coaps`.
- `CoapProtocolHandler` sends a confirmable request (block-wise through `Blockwise/` when the payload or response needs it) to port 5683, 5684 for `coaps` (or the URL's port), over `Secure/` for `coaps`, and writes the
  response payload. A 4.xx or 5.xx response ends with exit 0, or exit 22 under `-f` and
  `--fail-with-body`; `-i` writes `CoAP/1.0 c.dd` before the payload.
- `CoapRequest` gives the method (GET, POST with `-d`, PUT with `-T`, or `-X` GET, POST, PUT or DELETE; any other is
  exit 2) and the Uri-Host, Uri-Path and Uri-Query options (RFC 7252 section 6.4).
- `CoapExchange` retransmits at ACK_TIMEOUT times a random factor, doubling up to MAX_RETRANSMIT times
  on the transfer's `TimeProvider` (exit 28 after), takes a piggybacked or a separate response matched
  by token (sections 5.2 and 5.3), acknowledges a confirmable separate response, and fails with exit 56
  on a Reset.

`Secure/` (namespace `Curl.Extended.Protocol.Coap.Secure`, ADR-1047) secures `coaps://` (RFC 7252 section 9.1):
`DtlsDatagramChannel` runs the `Dtls/Handshake` client handshake over the UDP channel (which the handler disposes,
whether or not the handshake succeeds) and then carries each CoAP datagram as one protected application data record, so `CoapExchange`,
`Blockwise/` and `Observe/` run over it unchanged.

`Observe/` (namespace `Curl.Extended.Protocol.Coap.Observe`, ADR-1064) observes a resource (RFC 7641):

- `CoapObserveTransferHook` declares `--coap-observe`, registered in `ExtensionOptionCatalogue.cs` and
  `ExtensionTransferHookCatalogue.cs`, and hands a `coap://` or `coaps://` transfer on as a `CoapObserveContext`, which
  forwards the transfer's own context and tells `CoapProtocolHandler` to observe.
- `CoapObservation` adds Observe 0 to the GET; when the answer is a 2.xx with Observe, writes it and each later
  notification (payload, then a newline; under `-i` a `CoAP/1.0 c.dd` line first), drops one
  `CoapNotificationOrder` calls older (section 3.4), acknowledges confirmable ones, resets other messages,
  ends after a notification without Observe or not a 2.xx, re-registers (a non-confirmable GET with Observe 0
  and the same token) when the last notification's Max-Age (default 60 seconds) and 5 seconds pass without
  another, fetches the later blocks of a notification carrying Block2 with M set through `CoapBlockwiseTransfer`
  (without Observe, under a token of their own) and writes it once, whole, and on cancellation (`--max-time`, interruption)
  sends a GET with Observe 1 and the registration's token. An answer without Observe is written as a plain GET's.

`Blockwise/` (namespace `Curl.Extended.Protocol.Coap.Blockwise`, ADR-1057) carries large bodies (RFC 7959):

- `CoapBlockwiseTransfer` sends a payload over 1024 bytes in Block1 blocks, the next after each 2.31
  Continue, at the smaller size a 2.31 asks for, and again from block 0 at the smaller size a 4.13 asks
  for; then fetches a Block2 response block by block at the server's size, and gives it as one
  response whose payload holds every block once, in order (exit 56 for a block out of order or short).

`Wire/` (namespace `Curl.Extended.Protocol.Coap.Wire`) holds the message format:

- `CoapMessageEncoding` writes and reads a `CoapMessage` (RFC 7252 section 3): the 4-byte header
  (version 1, `CoapMessageType`, token length, `CoapCode`, Message ID), a token of 0 to 8 bytes, the
  options as `CoapOption`s with delta and length encoding and the 13 and 14 extended forms (section
  3.1), and the 0xFF payload marker before a non-empty payload. `Encode` writes options in ascending
  number. `Decode` throws `FormatException` for the message format errors section 3 names: version
  not 1, token length 9 to 15, a delta or length nibble of 15 that is not the payload marker, a
  payload marker with no payload, an Empty message with bytes after its header, and a message that
  ends inside its token or an option.
- `CoapCode` is the Code byte, class and detail written `c.dd`, with the section 12.1 codes.
- `CoapOptionNumber` lists the option numbers of RFC 7252 section 5.10, RFC 7959 and RFC 7641.
- `CoapUnsignedInteger` is the `uint` option value format (section 3.2); `CoapBlockValue` the
  Block1/Block2 value NUM, M and SZX (RFC 7959 section 2.2); `CoapObserveValue` the Observe value
  (RFC 7641 section 2).

`Dtls/Wire/` (namespace `Curl.Extended.Protocol.Coap.Dtls.Wire`) frames DTLS 1.2 (RFC 6347, ADR-1042):

- `DtlsRecordHeader` is the 13-byte record header (section 4.1): content type, version, epoch,
  48-bit sequence number and length, and `AdditionalData` the AEAD additional data built from the
  64-bit epoch and sequence number (section 4.1.2.1).
- `DtlsReplayWindow` is the 64-number anti-replay window (section 4.1.2.6).
- `DtlsHandshakeHeader` is the 12-byte handshake header (section 4.2.2); `DtlsHandshakeFragment`
  a header with its body bytes, split from a message to a size limit and read one after another
  from a record (section 4.2.3); `DtlsHandshakeReassembly` rebuilds one message from fragments
  arriving out of order, repeated or overlapping.
- `DtlsHelloVerifyRequest` is the HelloVerifyRequest body, server version and cookie (section 4.2.1).

Each throws `FormatException` for bytes the RFC does not allow.

`Dtls/Record/` (namespace `Curl.Extended.Protocol.Coap.Dtls.Record`) protects DTLS 1.2 records with AES-128-GCM or AES-128-CCM_8
(RFC 5288, RFC 6655, RFC 7251, RFC 6347 section 4.1.2):

- `DtlsKeyBlock` derives both sides' 16-byte keys and 4-byte write IVs with `TlsPrf.Sha256`.
- `DtlsRecordNonce` is the nonce: the write IV, then the 8-byte explicit part, the epoch and
  sequence number, sent before the ciphertext.
- `DtlsRecordProtector` writes one epoch's records, numbered from 0, with the tag over
  `DtlsRecordHeader.AdditionalData`; `DtlsRecordUnprotector` reads them, passing an epoch-0
  record through and dropping one that does not parse, is of another epoch, is a replay or fails its tag.

`Dtls/Retransmission/` (namespace `Curl.Extended.Protocol.Coap.Dtls.Retransmission`) resends handshake flights
(RFC 6347 section 4.2.4, ADR-1061): `DtlsFlightRetransmitter` sends a flight's datagrams in order and
resends them on a timer that starts at 1 second and doubles up to 60 seconds on the transfer's
`TimeProvider`, stops when the peer's next flight starts, and throws `TimeoutException` after 12 resends
(the handshake's caller turns it into exit 35).

`Dtls/Handshake/` (namespace `Curl.Extended.Protocol.Coap.Dtls.Handshake`, ADR-1078) runs the DTLS 1.2 client handshake
(RFC 6347 section 4.2, RFC 5246 section 7.3):

- `DtlsClientHandshake` sends a `DtlsClientHello` (TLS_ECDHE_ECDSA and TLS_ECDHE_RSA with AES-128-GCM, TLS_ECDHE_ECDSA with AES-128-CCM_8, secp256r1,
  server_name for a host name), again with a HelloVerifyRequest's cookie; takes ServerHello, Certificate,
  ServerKeyExchange and ServerHelloDone through `DtlsHandshakeMessageReader` (message_seq order, fragments rebuilt,
  repeats dropped); hands the chain to `ITlsProvider.VerifyServerCertificateChain` (a rejection fails with its exit
  code, 60 for an untrusted chain; `-k` accepts); checks the ECDHE signature with `DtlsServerKeyExchangeSignature`;
  sends ClientKeyExchange, ChangeCipherSpec and the protected Finished and checks the server's Finished over the
  transcript of section 4.2.6. Each flight goes through `DtlsFlightRetransmitter`; every other failure is exit 35.
- `DtlsClientSession` then sends and receives application data under epoch 1, dropping unprotected records,
  replays and records that fail their tag. `DtlsDatagramRecords` splits a datagram into records and a record into
  handshake fragments, dropping what does not parse.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extended.Protocol.Coap.UnitTests/Blockwise`, `Curl.Extended.Protocol.Coap.UnitTests/Observe`,
`Curl.Extended.Protocol.Coap.UnitTests/Wire`, `Curl.Extended.Protocol.Coap.UnitTests/Client`, `Curl.Extended.Protocol.Coap.UnitTests/Secure`, `Curl.Extended.Protocol.Coap.UnitTests/Dtls/Wire`,
`Curl.Extended.Protocol.Coap.UnitTests/Dtls/Record`, `Curl.Extended.Protocol.Coap.UnitTests/Dtls/Retransmission` and
`Curl.Extended.Protocol.Coap.UnitTests/Dtls/Handshake` (which runs the client against `Dtls/TestServer`).
