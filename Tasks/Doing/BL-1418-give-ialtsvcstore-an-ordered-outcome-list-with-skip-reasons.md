---
id: BL-1418
title: Give IAltSvcStore an ordered outcome list with skip reasons and add the IHstsStore seam (ADR-0409)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console/AltSvcTransferCache.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Fakes/ScriptedAltSvcStore.cs]
requirement: none
created: 2026-10-03
completed:
---
# BL-1418 — Give IAltSvcStore an ordered outcome list with skip reasons and add the IHstsStore seam (ADR-0409)

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` carries ADR-0409's contract: `IAltSvcStore.StoreFromResponse` returns one `AltSvcHeaderOutcome` per alternative read (added alternative or `AltSvcSkipReason`), and a new `IHstsStore` is settable on `HttpRequestOptions.HstsStore`, with no behaviour change yet.

## Context

- ADR-0409 (Documentation/Planning/Decisions) is the design; BL-1407 decided it.
- `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs` returns `IReadOnlyList<AltSvcAlternative>` today; `HttpRequestOptions.AltSvcStore` is the existing property beside which `HstsStore` goes.
- Implementers and callers that must keep compiling: `Curl.Console/AltSvcTransferCache.cs` (wraps each added alternative as an outcome), `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `StoreAltSvc` (reports `Added alt-svc:` only for added outcomes, as today), `Curl.Protocol.Http.UnitTests/Fakes/ScriptedAltSvcStore.cs`.

## Acceptance criteria

- [ ] `AltSvcSkipReason` has exactly `BadHostname`, `BadIpv6Hostname`, `UnknownPortNumber`, and `AltSvcHeaderOutcome` holds either an `AltSvcAlternative` or a reason, never both, with XML docs naming curl 8.21.0's `lib/altsvc.c` text for each reason.
- [ ] `IAltSvcStore.StoreFromResponse` returns `IReadOnlyList<AltSvcHeaderOutcome>` in header order.
- [ ] `IHstsStore.StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now)` returns `bool`, documented as `false` only when curl writes `Illegal STS header skipped`; `HttpRequestOptions.HstsStore` is `IHstsStore?`, default `null`.
- [ ] Every existing `-v` output is unchanged: the fast tests pass with no test expectation edited.
- [ ] 100% line and branch coverage of the new Abstractions types (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`).

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
