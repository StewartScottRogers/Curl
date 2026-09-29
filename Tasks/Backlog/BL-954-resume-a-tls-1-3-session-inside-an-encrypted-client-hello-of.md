---
id: BL-954
title: Resume a TLS 1.3 session inside an Encrypted Client Hello offer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-706]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-954 — Resume a TLS 1.3 session inside an Encrypted Client Hello offer

## Goal

A `Tls13ClientHandshake` with both `EncryptedClientHelloConfigs` (a supported config) and a resumable `ResumptionSession` offers the ticket in the inner ClientHello and a GREASE `pre_shared_key` in the outer one (RFC 9849 section 6.1.2), and resumes when the server accepts ECH and the ticket.

## Context

- ADR-0233 decision 7: BL-706 leaves resumption out of an ECH offer (`Tls13ClientHandshake.Start` skips `OfferSession` when `ech` is set).
- RFC 9849 section 6.1.2: when the inner hello carries `pre_shared_key`, the outer one SHOULD carry a GREASE `pre_shared_key` with random identities and binders of the same lengths, and `early_data` only if the inner one does; section 10.12.3 on why.
- The binder is computed over the inner hello (and, after a HelloRetryRequest, the inner transcript); a ServerHello's `pre_shared_key` is judged against the hello the server answered: accepted ECH resumes, a rejected ECH never does (the outer PSK is GREASE, so a `pre_shared_key` there is `illegal_parameter`).
- Start in `Curl.Tls.UnitLibrary/EchClientHello.cs` (`Build`) and `Tls13ClientHandshake.cs` (`OfferSession`, `BindPsk`, `ReadSelectedIdentity`); tests beside `Tls13EncryptedClientHelloTests` with `EchTestFrontEnd`, `Tls13TestServer.Tickets` and `ConfirmEch`.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` resume through an accepted ECH offer (the front end sees the ticket in the inner hello and a GREASE `pre_shared_key` of the same lengths in the outer one, and `IsResumed` is true), and a rejection with a `pre_shared_key` in the ServerHello is `illegal_parameter`.
- [ ] Early data offered with an ECH offer goes under the inner hello's early secret, or is not offered, as the ADR this task updates says.
- [ ] ADR-0233 decision 7 is updated to say what is built.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
