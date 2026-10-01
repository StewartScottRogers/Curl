---
id: BL-1064
title: Sign the client CertificateVerify with Ed448 and ML-DSA keys in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1064 — Sign the client CertificateVerify with Ed448 and ML-DSA keys in the hand-built TLS client

## Goal

A client certificate whose key is Ed448 or ML-DSA-44/65/87 signs the TLS 1.3 (and, for Ed448, TLS 1.2) CertificateVerify in the hand-built TLS client, as OpenSSL-built curl's `--cert` does.

## Context

- BL-940 made `TlsCertificatePublicKey` verify `ed448` (0x0808) and `mldsa44/65/87` (0x0904-0x0906), but the only `TlsSigningKey`s are `RsaTlsSigningKey`, `EcdsaTlsSigningKey` and `Ed25519TlsSigningKey`, so no client key can sign them.
- Add `Ed448TlsSigningKey` (over `Curl.Cryptography.Ed448.Sign`, as `Ed25519TlsSigningKey` is over `Ed25519`) and `MlDsaTlsSigningKey` (over `Curl.Cryptography.MlDsa.SignData` with an empty context), whose `Fits` accept `TlsSignatureKind.Ed448` and `TlsSignatureKind.MlDsa` with the key's own OID.
- How `--cert` loads such a key (PEM `PRIVATE KEY` with OID `1.3.101.113` or `2.16.840.1.101.3.4.3.17/.18/.19`) is in `Curl.Networking.UnitLibrary`; if it cannot yet, file that as its own task.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` complete a TLS 1.3 handshake whose server asks for a client certificate and receives a CertificateVerify signed by an Ed448 key and by each ML-DSA parameter set, verified by `TlsCertificatePublicKey`; and a TLS 1.2 one signed by an Ed448 key.
- [x] A signing key refuses (`CanSign` false) every scheme that does not fit its type and parameter set.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `Ed448TlsSigningKey(byte[] seed)` mirrors `Ed25519TlsSigningKey`; `MlDsaTlsSigningKey(MlDsa)` takes the key pair as `EcdsaTlsSigningKey` takes an `ECDsa`, and fits only the `mldsa*` scheme whose rule names its parameter set's OID.
- ML-DSA signs hedged (`SignData` with fresh randomness), FIPS 204's default and what OpenSSL does; the deterministic variant buys nothing here. Too small a choice for an ADR.
- The test credentials `TestServerCredential.Ed448` and `.MlDsa` now sign with the new keys, so the `ContentSigner` workaround BL-940 added is gone; the TLS 1.3 test server records `ClientCertificateVerifyScheme`.
- Tests: `Tls13ClientHandshakeTests.HandshakeSignsTheClientCertificateVerifyWithAnEd448OrMlDsaKey` (4 rows), `Tls12ClientHandshakeTests.AnEd448ClientCertificateSignsTheCertificateVerifyWithEd448`, `TlsSignatureTests.AnEd448KeySignsOnlyTheEd448SchemeAndIsFiftySevenBytes`, `TlsSignatureTests.AnMlDsaKeySignsOnlyItsParameterSetsScheme` (3 rows).
- `Measure-CodeQuality.ps1` flagged `Tls13ClientHandshake.ReadSelectedIdentity` at complexity 12 (from BL-701, not this task); the ticket check moved to `SelectsTheOfferedTicket`, so the library reports 0 failing members.
- Loading such a key from `--cert` is `Curl.Networking.UnitLibrary`'s job and filed as BL-1065.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Ed448 and ML-DSA-44/65/87 client keys sign the TLS 1.3 CertificateVerify, Ed448 the TLS 1.2 one
