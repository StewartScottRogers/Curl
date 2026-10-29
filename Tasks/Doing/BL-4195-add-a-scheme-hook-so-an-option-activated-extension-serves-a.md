---
id: BL-4195
title: Add a scheme hook so an option-activated extension serves a curl scheme (ws, wss) in place of curl's handler
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4173, BL-4192]
touches: [Curl.Extension.Abstractions.UnitLibrary/Scheme, Curl.Extension.Abstractions.UnitTests/Scheme, Curl.Extension.Composition.UnitLibrary/ExtensionSchemeHookCatalogue.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionSchemeHookTests.cs, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed:
---
# BL-4195 — Add a scheme hook so an option-activated extension serves a curl scheme (ws, wss) in place of curl's handler

## Goal

An extension whose option is given can serve a transfer for one of curl's own schemes (`ws://`, `wss://`) in place of curl's handler; without the option, curl's handler serves it unchanged.

## Context

Found by BL-4112's first pass: `Curl.Extension.Abstractions` has transfer hooks (headers, response body, result) and connection hooks (connector), but nothing lets an extension take a whole transfer for an existing scheme, and `ExtensionCatalogue` handlers are registered by scheme beside curl's. BL-4112 needs `ws`/`wss` run as RFC 8441/9220 extended CONNECT when its option is given. Follow the shape of `Connection/ConnectionHooks.cs` (ADR-1010): one active hook at most, refuse two with an `ArgumentException` naming the options. Waits on BL-4173 and BL-4192, which also change `Curl.Console/CurlComposition.cs`.

## Acceptance criteria

- [ ] `Curl.Extension.Abstractions.UnitLibrary/Scheme` holds an interface by which an extension names the schemes it serves when its option is given and supplies the handler, and a catalogue that keeps the hook whose option was given; unit tests.
- [ ] `Curl.Console` passes the activated scheme hook to the engine, which uses its handler for those schemes and curl's otherwise; with `CURL_EXTENSIONS=0` nothing changes; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the hook's shape.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a262fa8a f6b43e01 7da2aab5 478f867b 3d836d5e.
