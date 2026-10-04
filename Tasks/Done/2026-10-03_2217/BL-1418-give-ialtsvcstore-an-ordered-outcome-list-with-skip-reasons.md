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
completed: 2026-10-03
---
# BL-1418 — Give IAltSvcStore an ordered outcome list with skip reasons and add the IHstsStore seam (ADR-0409)

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` carries ADR-0409's contract: `IAltSvcStore.StoreFromResponse` returns one `AltSvcHeaderOutcome` per alternative read (added alternative or `AltSvcSkipReason`), and a new `IHstsStore` is settable on `HttpRequestOptions.HstsStore`, with no behaviour change yet.

## Context

- ADR-0409 (Documentation/Planning/Decisions) is the design; BL-1407 decided it.
- `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs` returns `IReadOnlyList<AltSvcAlternative>` today; `HttpRequestOptions.AltSvcStore` is the existing property beside which `HstsStore` goes.
- Implementers and callers that must keep compiling: `Curl.Console/AltSvcTransferCache.cs` (wraps each added alternative as an outcome), `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `StoreAltSvc` (reports `Added alt-svc:` only for added outcomes, as today), `Curl.Protocol.Http.UnitTests/Fakes/ScriptedAltSvcStore.cs`.

## Acceptance criteria

- [x] `AltSvcSkipReason` has exactly `BadHostname`, `BadIpv6Hostname`, `UnknownPortNumber`, and `AltSvcHeaderOutcome` holds either an `AltSvcAlternative` or a reason, never both, with XML docs naming curl 8.21.0's `lib/altsvc.c` text for each reason.
- [x] `IAltSvcStore.StoreFromResponse` returns `IReadOnlyList<AltSvcHeaderOutcome>` in header order.
- [x] `IHstsStore.StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now)` returns `bool`, documented as `false` only when curl writes `Illegal STS header skipped`; `HttpRequestOptions.HstsStore` is `IHstsStore?`, default `null`.
- [x] Every existing `-v` output is unchanged: the fast tests pass with no test expectation edited.
- [x] 100% line and branch coverage of the new Abstractions types (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`).

## Notes

- `AltSvcHeaderOutcome` is a sealed record with a private constructor and two factories, `Adding(AltSvcAlternative)` and `Skipping(AltSvcSkipReason)`, so it can never hold both; properties `Added` and `SkipReason`. Chosen over a public two-argument record so "never both" is enforced by the type, not by callers.
- `HttpProtocolHandler.StoreAltSvc` takes `outcomes.Select(o => o.Added).OfType<AltSvcAlternative>()`, so skipped outcomes write nothing yet (BL-1420 adds their text) and no new branch enters Curl.Protocol.Http.UnitLibrary. `AltSvcTransferCache` wraps each added entry with `Adding`; `ScriptedAltSvcStore` wraps its scripted alternatives the same way, so no Http test changed.
- `HttpRequestOptions.HstsStore` is documented as a seam no handler reads until BL-1420.
- Measure-CodeQuality (Abstractions, Http, Console in one run): Abstractions 100/100, Console 100/100, Http 100 line / 99.95 branch - the one gap is `HttpContentLength.TryParseItem` (BL-1387's code, untouched here), filed as BL-1429.
- Fast tests all green, no test expectation edited; new tests `AltSvcHeaderOutcomeTests` and `IHstsStoreTests`.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. IAltSvcStore returns ordered AltSvcHeaderOutcome lists with skip reasons, and IHstsStore is settable on HttpRequestOptions.HstsStore (ADR-0409)
