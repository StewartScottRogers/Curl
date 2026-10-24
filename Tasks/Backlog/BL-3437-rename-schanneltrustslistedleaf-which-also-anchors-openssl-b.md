---
id: BL-3437
title: Rename SchannelTrustsListedLeaf, which also anchors OpenSSL-build partial chains
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3437 — Rename SchannelTrustsListedLeaf, which also anchors OpenSSL-build partial chains

## Goal

The certificate-trust member that lets a `--cacert` file anchor a partial chain has a name and comments that describe both builds.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710). The code reviewer found it in the diff `79ec91e50...7f4970f66` (BL-3289, BL-3335, ADR-0797).

In `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`:

- **What the code does.** `SchannelTrustsListedLeaf` (line 365) runs for both builds. When `matchesSchannelBuild` is false, `ListsAnAnchorCandidate` (line 372) checks every chain element, not only the leaf. So on the OpenSSL build (Linux, macOS), any listed leaf or intermediate removes `RemoteCertificateChainErrors`.
- **That behaviour is intended.** It is the measured behaviour of OpenSSL's `X509_V_FLAG_PARTIAL_CHAIN`.
- **What the names claim.** The member name says "Schannel" and "Leaf". The comment on `WithoutChainErrorsCurlTolerates` (lines 346-348) says "when the Schannel build's --cacert file lists the server's own certificate".
- **Why it matters.** A reader auditing trust decisions would wrongly conclude the mask applies only on Windows and only to the leaf. That breaks CLAUDE.md's "Say what it does, do what it says", in security-sensitive code.

This is a rename and a comment fix only. Behaviour does not change.

## Acceptance criteria

- [ ] `SchannelTrustsListedLeaf` is renamed to a name true of both builds, e.g. `CaCertificateFileAnchorsChain`, and no reference to the old name remains in the repository.
- [ ] The comment on `WithoutChainErrorsCurlTolerates` describes both builds: the leaf on Schannel, and any chain certificate on OpenSSL.
- [ ] `dotnet build` is clean and the fast tests pass unchanged.

## Notes

## Log

- 2026-10-23: Created.
