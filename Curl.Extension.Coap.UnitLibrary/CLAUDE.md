# Curl.Extension.Coap.UnitLibrary

The CoAP extension (ADR-0999), tested against RFC 7252, RFC 7959, RFC 7641 and RFC 6347, not
against curl. It references only `Curl.Extension.Abstractions` (and through it
`Curl.Protocol.Abstractions`), the shared layer `Curl.Tls` (for `TlsContentType` and
`HandshakeType`, ADR-1040) and the base class library. No handler and no catalogue line yet: BL-4246 registers it.

`Wire/` (namespace `Curl.Extension.Coap.Wire`) holds the message format:

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

`Dtls/Wire/` (namespace `Curl.Extension.Coap.Dtls.Wire`) frames DTLS 1.2 (RFC 6347, ADR-1040):

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

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Coap.UnitTests/Wire` and `Curl.Extension.Coap.UnitTests/Dtls/Wire`.
