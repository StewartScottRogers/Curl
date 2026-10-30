# Curl.Extension.ObliviousHttp.UnitLibrary

The Oblivious HTTP extension (ADR-0999, ADR-1104), tested against RFC 9458 and RFC 9180. Namespace
`Curl.Extension.ObliviousHttp`. It references only `Curl.Cryptography.UnitLibrary`, a shared layer, and is not yet
registered in any Composition catalogue (BL-4137 does that).

- `Hpke/` is HPKE base mode (RFC 9180 sections 5.1, 5.2 and 5.3) over `Curl.Cryptography`'s `Hpke`, which holds the
  KEM, key schedule and AEADs: `HpkeSender.SetUp` (a fresh ephemeral key, or a given one for test vectors) gives the
  encapsulated key, `Seal` and `Export`; `HpkeReceiver.SetUp` gives `Open` and `Export`. `HpkeSuite` names the KEM,
  KDF and AEAD. An unusable peer key throws `HpkeSetupException`, a message that does not authenticate
  `HpkeOpenException`, both `CryptographicException`s.

Tested against the specifications, not against curl, with RFC 9180 Appendix A.1.1 and A.2.1. Held to the four quality
gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.ObliviousHttp.UnitTests`.
