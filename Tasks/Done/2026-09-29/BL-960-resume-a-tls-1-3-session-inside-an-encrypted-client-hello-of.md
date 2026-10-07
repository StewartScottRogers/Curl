---
id: BL-960
title: Resume a TLS 1.3 session inside an Encrypted Client Hello offer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-706]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0233-the-hand-built-tls-1-3-client-offers-ech-to-rfc-9849-with-shared-key-shares-and-boringssl-style-grease.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-960 — Resume a TLS 1.3 session inside an Encrypted Client Hello offer

## Goal

A `Tls13ClientHandshake` with both `EncryptedClientHelloConfigs` (a supported config) and a resumable `ResumptionSession` offers the ticket in the inner ClientHello and a GREASE `pre_shared_key` in the outer one (RFC 9849 section 6.1.2), and resumes when the server accepts ECH and the ticket.

## Context

- ADR-0233 decision 7: BL-706 leaves resumption out of an ECH offer (`Tls13ClientHandshake.Start` skips `OfferSession` when `ech` is set).
- RFC 9849 section 6.1.2: when the inner hello carries `pre_shared_key`, the outer one SHOULD carry a GREASE `pre_shared_key` with random identities and binders of the same lengths, and `early_data` only if the inner one does; section 10.12.3 on why.
- The binder is computed over the inner hello (and, after a HelloRetryRequest, the inner transcript); a ServerHello's `pre_shared_key` is judged against the hello the server answered: accepted ECH resumes, a rejected ECH never does (the outer PSK is GREASE, so a `pre_shared_key` there is `illegal_parameter`).
- Start in `Curl.Tls.UnitLibrary/EchClientHello.cs` (`Build`) and `Tls13ClientHandshake.cs` (`OfferSession`, `BindPsk`, `ReadSelectedIdentity`); tests beside `Tls13EncryptedClientHelloTests` with `EchTestFrontEnd`, `Tls13TestServer.Tickets` and `ConfirmEch`.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` resume through an accepted ECH offer (the front end sees the ticket in the inner hello and a GREASE `pre_shared_key` of the same lengths in the outer one, and `IsResumed` is true), and a rejection with a `pre_shared_key` in the ServerHello is `illegal_parameter`.
- [x] Early data offered with an ECH offer goes under the inner hello's early secret, or is not offered, as the ADR this task updates says.
- [x] ADR-0233 decision 7 is updated to say what is built.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Touches widened to ADR-0233 and the Decisions `README.md` row: the acceptance criteria update decision 7, and no task in Doing names either file.
- Built: `Start` always calls `OfferSession`; `EchClientHello.Build` takes the PSK offer and a binder callback, binds the inner hello over the handshake's transcript (the inner one, since the transcript follows the inner hello until a rejection), and `BuildOuter` gives the outer hello a GREASE `pre_shared_key` (random identity of the ticket's length, random age, random binder of the binder's length, drawn in that order) with `early_data` exactly when the inner one has it.
- `ReceiveKeyShare` now reads the ECH confirmation before the selected identity, so a ServerHello that rejected ECH and still selects a PSK is `illegal_parameter`.
- Early data (decided, recorded in ADR-0233 decision 7): offered as without ECH, under the inner hello's early secret; `clientHelloBytes` already holds the inner hello, so the early traffic secret needed no new code. The test checks it equals the backend's.
- After a rejection at the HelloRetryRequest, the second inner hello's binder is computed over the outer transcript; nobody can check it (the server cannot open the inner hello), so no special case.
- Delivered in-session rather than through the full `/feature` agent chain: one library and its tests, no CLI surface, so no conformance stage (nothing curl prints changes) and no `--ai-help` change.
- Tests: six new in `Tls13EncryptedClientHelloTests` replace `AnEchOfferDoesNotOfferTheSessionToResume`; `Curl.Tls.UnitTests` 1176 passing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. An ECH offer resumes: ticket in the inner hello, GREASE pre_shared_key in the outer, early data under the inner early secret
