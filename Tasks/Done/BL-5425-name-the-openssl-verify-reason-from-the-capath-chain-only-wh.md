---
id: BL-5425
title: Name the OpenSSL verify reason from the --capath chain only when it reaches an anchor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5425 — Name the OpenSSL verify reason from the --capath chain only when it reaches an anchor

## Goal

In the OpenSSL build with `--capath`, the exit 60 message and `%{ssl_verify_result}` name the system-store chain's reason when the server's chain does not lead to a `--capath` root.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (code-reviewer agent), in BL-5395's change.
`Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:186-189`: `ReasonChainFromAnchors` rebuilds the chain with `CustomRootTrust` over the anchors alone whenever chain errors remain, and `ServerCertificateVerification.cs:300` and `:465` use it in place of the system chain. For a server under a public CA (not in `--capath`) with an expired leaf, the rebuilt chain carries `UntrustedRoot` or `PartialChain`, which `OfChainStatus` ranks above validity, so curl's "certificate has expired" (10) becomes an untrusted-chain reason. The OpenSSL build still trusts its default CA bundle beside `--capath` (the parameter is named `anchorsBesideSystemStore`).

## Acceptance criteria

- [x] `ReasonChainFromAnchors` returns the rebuilt chain only when it ends at one of the anchors; otherwise it returns null, so the system-store chain names the reason.
- [x] A unit test: an expired leaf under a CA outside `--capath` (trusted as a system root in the test) reports verify result 10 and its message; BL-5395's test (an expired leaf under a `--capath` root) still reports 10.
- [x] Quality gates hold.

## Notes

- ReasonChainFromAnchors now disposes and returns null unless the rebuilt chain ends at an anchor (thumbprint). Test added in RebuiltChainVerifyReasonTests. Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line, 100% branch, 0 failing; its Networking tests pass.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 4d76b3fe 725f71d9 d17de588.
- 2026-11-18: Doing -> Done. ReasonChainFromAnchors names the reason only when the --capath chain reaches an anchor
