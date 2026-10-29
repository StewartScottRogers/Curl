---
id: BL-4267
title: Extension CoAP: re-register an observation on Max-Age and take block-wise notifications
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4248]
touches: [Curl.Extension.Coap.UnitLibrary/Observe, Curl.Extension.Coap.UnitTests/Observe]
requirement: none
created: 2026-10-29
completed:
---
# BL-4267 — Extension CoAP: re-register an observation on Max-Age and take block-wise notifications

## Goal

Under `--coap-observe`, Curl re-registers when the last notification's Max-Age runs out without a fresh one (RFC 7641 section 3.3.1), and takes a notification larger than one block by fetching its remaining blocks (RFC 7959 section 2.6), writing it once, whole.

## Context

Follows BL-4248, whose ADR-1064 left these two parts of RFC 7641 to this task. `Curl.Extension.Coap.Observe.CoapObservation` follows notifications; `Curl.Extension.Coap.Blockwise.CoapBlockwiseTransfer` fetches Block2 responses. Extension rules: ADR-0999; tested against the RFCs, not curl; held to the four quality gates.

## Acceptance criteria

- [ ] A unit test over a fake `IDatagramConnector` and a fake `TimeProvider` shows a GET with Observe 0 and the same token sent again once the last notification's Max-Age (default 60 s) plus a margin passes without a notification (RFC 7641 section 3.3.1).
- [ ] A unit test shows a notification carrying Block2 with M set written once, whole, after its later blocks are fetched without Observe (RFC 7959 section 2.6).
- [ ] ADR-1064 is updated to describe both behaviours.
- [ ] `Curl.Extension.Coap.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
