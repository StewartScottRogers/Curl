---
id: BL-985
title: Record BL-980's SHA-3 tls-server-end-point measurement and decision in ADR-0234
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-980]
touches: [Documentation/Planning/Decisions/ADR-0234-negotiate-carries-the-https-server-certificate-to-the-hand-built-kerberos-for-tls-server-end-point-bindings.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-985 — Record BL-980's SHA-3 tls-server-end-point measurement and decision in ADR-0234

## Goal

ADR-0234 states, as a second amendment, what curl 8.18.0 (OpenSSL, MIT) was measured doing with SHA-3-signed server certificates and that `TlsServerEndPointChannelBindings` now binds them with the matching SHA-3 (BL-980).

## Context

- BL-980 made the change in code but could not edit ADR-0234: `Documentation/Planning/Decisions` was in BL-887's `touches` while it ran, so this amendment was split off. The measurement is in BL-980's Notes (Tasks/Done or its archive); copy it, do not re-measure.
- ADR-0234's BL-965 amendment ends with "Other signatures OpenSSL pairs with a digest the table lacks (the SHA-3 family) still fall to the unknown-OID failure until BL-980 measures and matches them." That sentence becomes untrue with BL-980 and must be replaced.

## Acceptance criteria

- [x] ADR-0234 has an "Amendment (BL-980)" section, marked "Decided by Claude under Stewart's delegation", with BL-980's measurement table (all eight RSA and ECDSA SHA3-224..512 certificates: Negotiate token sent, exit 0) and the decision: the eight OIDs 2.16.840.1.101.3.4.3.9-16 take SHA3-224/256/384/512 from `Curl.Cryptography`'s hand-built `Sha3`, pinned by `TlsServerEndPointChannelBindingsTests` to OpenSSL's hashes of two measured certificates.
- [x] The BL-965 amendment's closing sentence about the SHA-3 family no longer says they fail.

## Notes

- Filed by BL-980.
- Copied BL-980's measurement and design notes into ADR-0234 as "Amendment (BL-980)"; no re-measurement. Checked the eight OIDs and `Sha3.HashData*` calls in `TlsServerEndPointChannelBindings.cs` match what the amendment states. Replaced the BL-965 amendment's closing SHA-3 sentence with a pointer to the new amendment.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0234 records BL-980's SHA-3 tls-server-end-point measurement and decision as its BL-980 amendment
