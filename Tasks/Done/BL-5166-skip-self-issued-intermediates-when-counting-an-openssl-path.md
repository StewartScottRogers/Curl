---
id: BL-5166
title: Skip self-issued intermediates when counting an OpenSSL path length
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitTests/OpenSslVerifyResultTests.cs, Documentation/Planning/Decisions/ADR-1269-openssl-chain-checks-after-trust-name-their-verify-result.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5166 — Skip self-issued intermediates when counting an OpenSSL path length

## Goal

`OpenSslVerifyResult.ExceedsAPathLength` counts a CA's intermediates as OpenSSL's `check_chain_extensions` does, leaving self-issued intermediates out, and its doc comment and ADR-1269 say so truthfully.

## Context

Found by the shift-end review of 2026-11-13 (ADR-0710) - code-reviewer, with type-design-analyzer and silent-failure-hunter agreeing - in what BL-5104 merged.

`Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs` (`ExceedsAPathLength`, about lines 170-189) counts every certificate between the leaf and a CA (`depth - 1`) against the CA's path length constraint, and its doc comment says that is "as OpenSSL's `check_chain_extensions` counts them (self-issued intermediates counted too)". OpenSSL does the opposite: it increments `plen` only for a certificate that is not self-issued (`if (i > 0 && !(x->ex_flags & EXFLAG_SI)) plen++`), as RFC 5280 6.1.4(l) requires. ADR-1269 calls counting them "a simplification", but the doc comment states it as OpenSSL's behaviour, which is false ("say what it does, do what it says"). Effect: a chain .NET flags `InvalidBasicConstraints` that also holds a self-issued (key-rollover) intermediate under a `pathLen` CA gets `path length constraint exceeded (25)` where OpenSSL reports `invalid CA certificate (79)`. Self-issued means the certificate's subject equals its issuer (OpenSSL's `EXFLAG_SI`).

## Acceptance criteria

- [x] `ExceedsAPathLength` skips self-issued intermediates (subject equals issuer) when it counts the certificates below a CA; a unit test in `OpenSslVerifyResultTests.cs` builds a chain where only that self-issued intermediate would exceed the constraint and asserts `false`, and the existing exceeded case still asserts `true`.
- [x] The doc comment on `ExceedsAPathLength` and ADR-1269's paragraph on it describe the counting as the code now does it, with no claim that OpenSSL counts self-issued intermediates.
- [x] `dotnet build` clean, fast tests green, `Curl.Networking.UnitLibrary` at 100% line and branch coverage with no member over the complexity or CRAP gates.

## Notes

- Self-issued = subject RawData equals issuer RawData. Quality run: 0 failing members for Curl.Networking.UnitLibrary. Existing test chain intermediates now get a distinct issuer so they still count.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 773f45ce f16d41d3 493c6829.
- 2026-11-13: Doing -> Done. ExceedsAPathLength skips self-issued intermediates; doc and ADR-1269 corrected
