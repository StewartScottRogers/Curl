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
completed: 2026-11-18
---
# BL-5424 — Keep the name-constraint decode catch from refusing a chain with no constraints

## Goal

A server certificate whose own subjectAltName does not decode is refused as a name-constraint violation only when some CA in the chain carries a NameConstraints extension.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (silent-failure-hunter agent), in BL-5415's change.
`Curl.Networking.UnitLibrary/CertificateNameConstraints.cs:41-59`: the new `catch (AsnContentException or CryptographicException)` returning `NotPermitted` also wraps `ListNames(elements[0].Certificate)`, which reads the leaf's SAN before any CA's constraints are looked at. A leaf with an undecodable SAN under a chain with no name constraints now gets violation 0x4000. The Schannel build's `CaCertificateFileChainFailure` (`ServerCertificateVerification.ChainErrors.cs:100`) then names `schannel: error 0x00004000`, and `CaCertificateFileAnchorsChain` (`:137`, `FindViolation(chain) == 0`) stops treating the `--cacert` chain as anchored.

## Acceptance criteria

- [x] The leaf's names are read only once a CA with a NameConstraints extension is found, and the catch still covers a malformed constraint or name in that case.
- [x] A unit test: a leaf with a malformed SAN in a chain with no name constraints makes `FindViolation` return 0.
- [x] BL-5415's tests still pass; quality gates hold.

## Notes

- Leaf names are now read lazily, once a CA with NameConstraints is found; the 2-arg overload takes the extension (dead null check removed for coverage). Quality run on Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: c713d78f 17acec10 3f952434.
- 2026-11-18: Doing -> Done. Leaf names read only once a CA with NameConstraints is found; test added
