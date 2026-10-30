# Curl.Extension.ObliviousHttp.UnitLibrary

The Oblivious HTTP extension (ADR-0999, ADR-1104), tested against RFC 9458 and RFC 9180. Namespace
`Curl.Extension.ObliviousHttp`. It references only `Curl.Cryptography.UnitLibrary`, a shared layer, and is not yet
registered in any Composition catalogue (BL-4137 does that).

- `Hpke/` is HPKE base mode (RFC 9180 sections 5.1, 5.2 and 5.3) over `Curl.Cryptography`'s `Hpke`, which holds the
  KEM, key schedule and AEADs: `HpkeSender.SetUp` (a fresh ephemeral key, or a given one for test vectors) gives the
  encapsulated key, `Seal` and `Export`; `HpkeReceiver.SetUp` gives `Open` and `Export`. `HpkeSuite` names the KEM,
  KDF and AEAD. An unusable peer key throws `HpkeSetupException`, a message that does not authenticate
  `HpkeOpenException`, both `CryptographicException`s.
- `BinaryHttp/` is Binary HTTP (RFC 9292), the message format Oblivious HTTP encapsulates:
  `BinaryHttpRequestEncoder.EncodeKnownLength` writes a `BinaryHttpRequest` as a known-length request (framing
  indicator 0, field names in lower case, empty trailers); `BinaryHttpResponseDecoder.Decode` reads a known-length (1)
  or indeterminate-length (3) response into a `BinaryHttpResponse` with its informational responses, headers, content
  and trailers, treating truncated content and trailers as empty and checking padding is zero. Field names and values
  are Latin-1 strings. `QuicVariableLengthInteger` writes RFC 9000 section 16 integers and `BinaryHttpReader` reads
  them, here rather than taken from a protocol library. An invalid message throws `BinaryHttpFormatException`, a
  `FormatException`.
- `Encapsulation/` is RFC 9458 sections 3 and 4 (ADR-1113): `KeyConfiguration.Parse` reads one key configuration,
  `KeyConfigurationList.Parse` an `application/ohttp-keys` list (leaving out configurations whose KEM is not
  supported) and `SelectFirstSupported` gives the first configuration and KDF and AEAD pair this library implements as
  a `KeyConfigurationChoice`. `ObliviousRequest.Encapsulate` builds the `message/ohttp-req` body and keeps the HPKE
  context whose `DecapsulateResponse` opens the `message/ohttp-res` body. Bad bytes throw
  `MalformedKeyConfigurationException`, nothing usable `UnsupportedKeyConfigurationException`, a response that is
  short or does not authenticate `EncapsulatedResponseException`. `ObliviousHttpMediaTypes` names the media types.

Tested against the specifications, not against curl, with RFC 9180 Appendix A.1.1 and A.2.1, the RFC 9292
section 5 examples and RFC 9458 Appendix A. Held to the four quality
gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.ObliviousHttp.UnitTests`.
