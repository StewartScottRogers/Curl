---
id: BL-4271
title: Extension: XMPP SCRAM-SHA-1 and SCRAM-SHA-256 authentication
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4270]
touches: [Curl.Extension.Xmpp.UnitLibrary/Client, Curl.Extension.Xmpp.UnitTests, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4271 — Extension: XMPP SCRAM-SHA-1 and SCRAM-SHA-256 authentication

## Goal

The XMPP extension authenticates with SCRAM-SHA-1 and SCRAM-SHA-256, which ADR-1068 left undone.

## Context

Part of BL-4123 (Extension: Send XMPP messages and service queries), which landed PLAIN over STARTTLS and ANONYMOUS in `Curl.Extension.Xmpp.UnitLibrary`. Specification: RFC 5802, RFC 7677, RFC 6120 section 6. See ADR-1068. Use only the base class library (`HMACSHA1`, `HMACSHA256`, `Rfc2898DeriveBytes`); never reference a `Curl.Protocol.*` library or another extension.

## Acceptance criteria

- [x] SCRAM-SHA-1 and SCRAM-SHA-256 SASL mechanisms exist in `Curl.Extension.Xmpp.UnitLibrary/Client`, each as its own self-registering file, chosen over PLAIN when the server offers them.
- [x] Unit tests check the client proof and server signature against the RFC 5802 section 5 and RFC 7677 section 3 example exchanges, with no network.
- [x] A wrong server signature fails authentication with the extension's authentication-failure error; unit test.
- [x] ADR-1068 (or a new ADR marked "Decided by Claude under Stewart's delegation") records that SCRAM is now done.
- [x] `Curl.Extension.Xmpp.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4123.

- Plan: one self-registering file per SASL mechanism (`XmppSaslPlain`, `XmppSaslAnonymous`, `XmppScramSha1`,
  `XmppScramSha256`) adding itself to `XmppSaslMechanisms` from a `[ModuleInitializer]`, CA2255 suppressed for the
  namespace as `Curl.Conformance.Differential` does; `XmppScramExchange` holds RFC 5802's arithmetic and
  `XmppClientFlow` now carries any number of `<challenge/>` rounds. Decisions in ADR-1077 (a new ADR, so ADR-1068's
  file was left as it is).
- Default taken: the server signature is accepted in `<success/>` or in a second `<challenge/>` (some servers send
  it there); password used as UTF-8 without SASLprep (no stringprep in the BCL; ASCII unaffected).
- The "not offered" message now names every candidate: `XMPP server does not offer SASL SCRAM-SHA-256 or SCRAM-SHA-1 or PLAIN`.
- `XmppProtocolHandler` takes the client-nonce source so tests replay the RFC exchanges byte for byte.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Xmpp.UnitLibrary`: 100% line, 100% branch, max
  complexity within the gate, 0 failing members. Full build clean; fast tests green (71 test assemblies, 0 failed).
  Tests are platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d8e97e00 8e9a2876 687a7e2d 0f52d9a9 b582378e 3d836d5e.
- 2026-10-29: Doing -> Done. XMPP logs in with SCRAM-SHA-256 or SCRAM-SHA-1 before PLAIN, checking the server signature
