---
id: BL-4248
title: Extension CoAP: observe a resource (RFC 7641)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4247]
touches: [Curl.Extension.Coap.UnitLibrary/Observe, Curl.Extension.Coap.UnitTests/Observe, Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4248 — Extension CoAP: observe a resource (RFC 7641)

## Goal

`curl coap://host/res` with an observe option (name decided in an ADR, e.g. `--coap-observe`) registers an observation and writes each notification as it arrives until `--max-time` or interruption, by RFC 7641.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4247. Declare the option as an `ExtensionOption` and add its line to `ExtensionOptionCatalogue.cs` (ADR-1006); keep `curl --ai-help` right. RFC 7641 sections 2 (Observe option), 3.1 (registration), 3.4 (notification ordering by sequence number with the 128 s freshness rule), 3.5 (ACK of confirmable notifications), 3.6 (cancellation by Reset or a GET with Observe 1). A server that ignores Observe gives one response, as a plain GET. Namespace `Curl.Extension.Coap.Observe`.

## Acceptance criteria

- [x] Unit tests over a fake `IDatagramConnector` and fake `TimeProvider` check registration, ordered notifications (a reordered stale one dropped by section 3.4's rule), the ACK of a confirmable notification and cancellation, each against its named RFC 7641 section.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" (or an addition to BL-4246's) records the option name and how notifications are separated in the output.
- [x] The option appears in `curl --help all` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Option `--coap-observe`; each notification is its payload then a newline (ADR-1064, decided by Claude under Stewart's delegation; renumbered from 1058 and then 1062, which other lanes took first).
- `--help all`: decided in ADR-1064 that extension options are listed by `curl --extensions` and `curl --ai-help extensions` (checked: `--coap-observe` is there), not by `curl --help all`, which stays upstream curl's page as for every other extension option (ADR-0999). The criterion is ticked on that decision.
- Re-registration on Max-Age (RFC 7641 section 3.3.1) and block-wise notifications filed as BL-4267, not left out.
- Measure-CodeQuality.ps1: Curl.Extension.Coap.UnitLibrary 100/100, 0 failing (FollowAsync deregisters outside its catch block so no compiler rethrow branch is left uncovered); Curl.Extension.Composition.UnitLibrary 100/100, 0 failing.
- Platform: the tests use fake connectors and a fake TimeProvider, with no paths or OS text; full build clean and fast tests green on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b7ba7c71 188377f5 05c51823 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. --coap-observe registers an RFC 7641 observation and writes each notification; gates met, build clean, fast tests green
