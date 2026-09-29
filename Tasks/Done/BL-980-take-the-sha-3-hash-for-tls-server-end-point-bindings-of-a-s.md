---
id: BL-980
title: Take the SHA-3 hash for tls-server-end-point bindings of a SHA-3-signed server certificate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-965]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-980 — Take the SHA-3 hash for tls-server-end-point bindings of a SHA-3-signed server certificate

## Goal

Hand-built Negotiate over HTTPS does what curl 8.18.0 (OpenSSL, MIT) is measured doing when the server certificate is signed with RSA or ECDSA over SHA3-224, SHA3-256, SHA3-384 or SHA3-512, instead of failing with BL-965's unknown-OID exit 91.

## Context

- BL-965's `TlsServerEndPointChannelBindings.Of` (Curl.Authentication.UnitLibrary) maps signature OIDs to RFC 5929 hashes and throws `HttpAuthenticationFailedException` (exit 91, "Unable to find digest NID for certificate signature algorithm") for any OID outside its table and its PSS/EdDSA set (ADR-0234, amendment BL-965).
- OpenSSL pairs the SHA-3 signature OIDs (`id-rsassa-pkcs1-v1_5-with-sha3-*` 2.16.840.1.101.3.4.3.13-16, `id-ecdsa-with-sha3-*` 2.16.840.1.101.3.4.3.9-12) with a real digest, so curl presumably sends bindings hashed with that SHA-3. `SHA3_256.IsSupported` is false on macOS, so the hash must be hand-built (Keccak) in this library to pass on all three platforms.
- Measure as BL-965 did (ADR-0234 amendment): `openssl req -newkey rsa:2048 -sha3-256` certificate served by `openssl s_server -HTTP` in WSL to curl 8.18.0.

## Acceptance criteria

- [x] The measurement for an RSA SHA3-256 certificate is recorded under Notes below, and BL-983 carries it into ADR-0234 (amended again). (Was: "recorded in ADR-0234 or a new ADR"; split off, see Notes.)
- [x] `Curl.Authentication.UnitTests` tests pin a hand-built SHA-3 to NIST examples and the bindings for each SHA-3 signature OID curl is measured to accept. (The NIST examples are pinned in `Curl.Cryptography.UnitTests`' `Sha3Tests`, beside the reused `Sha3`; see Notes.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-965.
- **Measured** 2026-09-29, as BL-965 did: WSL Ubuntu's curl 8.18.0 (OpenSSL 3.5.5, `mit-krb5/1.22.1`) against `openssl s_server -HTTP` serving a 401 `WWW-Authenticate: Negotiate`, `curl -v -k --negotiate -u : https://server.example.test:18980/neg` with a ticket from the user-space MIT KDC (throwaway `~/bl980.sh` in WSL, nothing committed). Certificates from `openssl req -x509 -newkey rsa:2048 -sha3-N` and `-newkey ec -pkeyopt ec_paramgen_curve:P-256 -sha3-N`:

  | Server certificate's signature | curl 8.18.0 |
  | --- | --- |
  | RSA-SHA3-224, -256, -384, -512 (2.16.840.1.101.3.4.3.13-16) | Negotiate token sent; the transfer ends on the 401 with exit 0. |
  | ecdsa_with_SHA3-224, -256, -384, -512 (2.16.840.1.101.3.4.3.9-12) | The same, exit 0. |

  So OpenSSL pairs each with its SHA-3 digest and curl binds with it (RFC 5929: the signature's own hash). `openssl dgst -sha3-256` of the RSA SHA3-256 certificate is `c5829afc…4217`, `-sha3-512` of the ECDSA SHA3-512 one `2179cd4b…dae6`; both certificates and hashes are pinned in `TlsServerEndPointChannelBindingsTests`.
- **Design (decided by Claude under Stewart's delegation).** Reused `Curl.Cryptography`'s hand-built `Sha3` (Keccak, already there for ML-KEM and already a transitive dependency through `Curl.Kerberos`) instead of a second Keccak in `Curl.Authentication`: added `HashData224` and `HashData384` beside `HashData256`/`HashData512`, pinned to NIST's SHA-3 examples (empty message, "abc", the 1600-bit message) in `Sha3Tests`. `Curl.Authentication.UnitLibrary` now references `Curl.Cryptography.UnitLibrary` directly. The eight OIDs map to four private `Sha3_*` adapters in `TlsServerEndPointChannelBindings`. Added `Curl.Cryptography.UnitLibrary` and `Curl.Cryptography.UnitTests` to `touches`: no task in Doing names them.
- **ADR split off.** ADR-0234 sits in `Documentation/Planning/Decisions`, which BL-887 (in Doing, renumbering ADRs) has in its `touches`, so this task could not edit it without overlapping. Filed BL-983 (depends on BL-980, touches only the ADR-0234 file) to write the amendment from the measurement above, and reworded the first criterion to match.
- Results: build `-warnaserror` 0 warnings; fast tests all green (Curl.Authentication.UnitTests 714 passed, 4 skipped; Curl.Cryptography.UnitTests 1302 passed); Measure-CodeQuality: Curl.Authentication.UnitLibrary and Curl.Cryptography.UnitLibrary both 100% line, 100% branch, 0 failing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Hand-built Negotiate over HTTPS binds RSA and ECDSA SHA3-224..512-signed server certificates with their SHA-3, as curl 8.18.0 was measured accepting them
