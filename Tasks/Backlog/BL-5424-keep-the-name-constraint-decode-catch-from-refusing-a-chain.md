---
id: BL-5424
title: Keep the name-constraint decode catch from refusing a chain with no constraints
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5424 — Keep the name-constraint decode catch from refusing a chain with no constraints

## Goal

A server certificate whose own subjectAltName does not decode is refused as a name-constraint violation only when some CA in the chain carries a NameConstraints extension.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (silent-failure-hunter agent), in BL-5415's change.
`Curl.Networking.UnitLibrary/CertificateNameConstraints.cs:41-59`: the new `catch (AsnContentException or CryptographicException)` returning `NotPermitted` also wraps `ListNames(elements[0].Certificate)`, which reads the leaf's SAN before any CA's constraints are looked at. A leaf with an undecodable SAN under a chain with no name constraints now gets violation 0x4000. The Schannel build's `CaCertificateFileChainFailure` (`ServerCertificateVerification.ChainErrors.cs:100`) then names `schannel: error 0x00004000`, and `CaCertificateFileAnchorsChain` (`:137`, `FindViolation(chain) == 0`) stops treating the `--cacert` chain as anchored.

## Acceptance criteria

- [ ] The leaf's names are read only once a CA with a NameConstraints extension is found, and the catch still covers a malformed constraint or name in that case.
- [ ] A unit test: a leaf with a malformed SAN in a chain with no name constraints makes `FindViolation` return 0.
- [ ] BL-5415's tests still pass; quality gates hold.

## Notes

## Log

- 2026-11-18: Created.
