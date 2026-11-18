---
id: BL-5422
title: Fetch below-anchor CRLs once per verification under the connect deadline
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListDownload.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5422 — Fetch below-anchor CRLs once per verification under the connect deadline

## Goal

Checking revocation below a `--cacert` anchor fetches each CRL distribution point at most once per verification, within the connect's deadline and cancellation, and says why a fetch failed.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (silent-failure-hunter and code-reviewer agents).
`Curl.Networking.UnitLibrary/RevocationListDownload.cs:18-38` calls the synchronous `HttpClient.Send` with a fixed 5-second timeout and no `CancellationToken`, inside certificate validation. `ReadBelowAnchorRevocation` (`ServerCertificateVerification.AnchorRevocation.cs:41`, fetch at `:66`) runs from `CaCertificateFileAnchorsChain` (`ServerCertificateVerification.ChainErrors.cs:146`) and again from `RevokesBelowNonSelfSignedAnchor` (`:156`), each fetching every http distribution point of every certificate below the anchor with no cache. An unreachable point stalls the handshake 5 s x points x certificates x 2, past `--connect-timeout` and `-m` (ADR-0117's one connect limit), and Ctrl-C cannot cut it. Every failure collapses to `null` (Unresolved) with nothing on `-v` or the `tls` diagnostic log. The response body is read with no size limit.

## Acceptance criteria

- [ ] `ReadBelowAnchorRevocation`'s answer is worked out once per verification and reused; a unit test with a counting `FetchRevocationList` shows each URI fetched at most once.
- [ ] The fetch is bounded by the connect's remaining time or cancellation, and a unit test shows a stalled fetch does not extend the connect past its limit.
- [ ] A failed fetch is written to the `tls` diagnostic log at `warning`, and a unit test pins the line.
- [ ] The downloaded body is capped (for example 10 MB); a larger answer counts as unfetchable.
- [ ] Curl.Networking.UnitLibrary keeps its quality gates.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 69438f19 92f4726d 6eef085d 4e923170 9abf1d2b.
