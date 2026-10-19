---
id: BL-2682
title: Unfold a folded header's value in the header API so upstream case 1941 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferHeaderStore.cs, Curl.Engine.UnitTests/TransferHeaderStoreTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2682 — Unfold a folded header's value in the header API so upstream case 1941 passes

## Goal

Upstream case 1941 (`curl_easy_header with CONNECT`, run by lib1940) passes through the case runner and holds the ratchet.

## Context

Found by BL-2547. The case's `CONNECT` response folds a header (`Server: from ` continued on a line starting with a tab, `\tthe\tconnect`), and `curl_easy_header` must give its value as upstream 8.22.0 does, `from the connect`; Curl's gives `from the\tconnect`: `test1941 fails: <verify><stdout> differs at byte 282 (line 9): expected " Server == from the connect\n", got " Server == from the\tconnect\n"`. The header API's values come from `Curl.Engine.UnitLibrary/TransferHeaderStore.cs`; read upstream's `lib/headers.c` (`unfold_value`) for the exact unfolding and match it, with a focused unit test in `Curl.Engine.UnitTests` (ADR-0482). Run the case with `CURL_UPSTREAM_CASE=1941`.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests` shows a folded header's value unfolded as upstream's `unfold_value` does (a tab inside the continuation becoming what upstream makes it).
- [x] Upstream case 1941 passes through the case runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened, and every case on the ratchet still passes.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading TransferHeaderStore, so it can run beside other Engine tasks.
- 2026-10-18: Upstream 8.22.0 joins a fold with one space and drops all its leading and trailing blanks (test1941 expects `from the connect` from `from `, ` the\t`, `\t\tconnect`, and `text/html` from an empty `Content-Type:` folded with ` text/html`). `TransferHeaderStore.Unfold` now trims the fold and appends it after one space, or as the whole value when the value is empty; the old test pinning `one\ttwo\tthree` now pins `one two three`, plus two focused tests. Case 1941 passes and is on the ratchet; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: bdc9fca7 63afdf6b 4cf19161.
- 2026-10-18: Doing -> Done. curl_easy_header unfolds a folded header as upstream 8.22.0 (one space, no fold blanks); upstream case 1941 passes and holds the ratchet
