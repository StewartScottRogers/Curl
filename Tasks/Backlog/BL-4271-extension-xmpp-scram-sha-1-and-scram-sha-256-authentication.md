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
completed:
---
# BL-4271 — Extension: XMPP SCRAM-SHA-1 and SCRAM-SHA-256 authentication

## Goal

The XMPP extension authenticates with SCRAM-SHA-1 and SCRAM-SHA-256, which ADR-1068 left undone.

## Context

Part of BL-4123 (Extension: Send XMPP messages and service queries), which landed PLAIN over STARTTLS and ANONYMOUS in `Curl.Extension.Xmpp.UnitLibrary`. Specification: RFC 5802, RFC 7677, RFC 6120 section 6. See ADR-1068. Use only the base class library (`HMACSHA1`, `HMACSHA256`, `Rfc2898DeriveBytes`); never reference a `Curl.Protocol.*` library or another extension.

## Acceptance criteria

- [ ] SCRAM-SHA-1 and SCRAM-SHA-256 SASL mechanisms exist in `Curl.Extension.Xmpp.UnitLibrary/Client`, each as its own self-registering file, chosen over PLAIN when the server offers them.
- [ ] Unit tests check the client proof and server signature against the RFC 5802 section 5 and RFC 7677 section 3 example exchanges, with no network.
- [ ] A wrong server signature fails authentication with the extension's authentication-failure error; unit test.
- [ ] ADR-1068 (or a new ADR marked "Decided by Claude under Stewart's delegation") records that SCRAM is now done.
- [ ] `Curl.Extension.Xmpp.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4123.

## Log

- 2026-10-29: Created.
