---
id: BL-972
title: Number file:// transfers from the same connection count as networked ones
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-936]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console]
requirement: none
created: 2026-09-29
completed:
---
# BL-972 — Number file:// transfers from the same connection count as networked ones

## Goal

In a run that mixes `file://` with a networked scheme, `* shutting down connection #N` for a `file://` transfer carries the number curl 8.21.0 gives it: one count shared with every connection the run opens.

## Context

- BL-936 made `FileProtocolHandler` write `* shutting down connection #N` / `* closing connection #N`, numbering from its own counter (`lastConnectionNumber`), because the shared count lives in `Curl.Networking.UnitLibrary/PoolingConnector.cs` (`_nextConnectionNumber`) and no abstraction hands a number out without connecting.
- curl numbers `file://` transfers as connections in the one count (measured in BL-936: `curl -v a a nope a` over `file://` prints #0, #1, nothing for the failed open, #2). Measure a mix first, e.g. `curl -v http://127.0.0.1:<port>/ file:///<dir>/a.txt`, with `Record-CurlExchange.ps1`, and pin its answer.
- Likely shape: an abstraction (say `IConnectionNumbers` in `Curl.Protocol.Abstractions.UnitLibrary`) that `PoolingConnector` implements and `CurlComposition` passes to `FileProtocolHandler`.

## Acceptance criteria

- [ ] The measured stderr for a mixed `http://` + `file://` run is copied into Notes.
- [ ] A test pins that a `file://` transfer after a networked connection reports the shared next number.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] Coverage for each touched library stays at 100% line and branch (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-09-29: Created.
