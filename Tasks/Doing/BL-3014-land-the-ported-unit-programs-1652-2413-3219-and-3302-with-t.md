---
id: BL-3014
title: Land the ported unit programs 1652, 2413, 3219 and 3302 with their Core, Engine and Libcurl fixes: fix the unit2413 ipv6 row and commit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy, Curl.Conformance.UnitProgram.UnitTests/Dns/Easy, Curl.Core.UnitLibrary/ConnectionPeer.cs, Curl.Core.UnitTests/ConnectionPeerTests.cs, Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs, Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Libcurl.UnitTests/Info/Longs/EasyInfoReadersLongsTests.cs, Libcurl/Libcurl.csproj]
requirement: none
created: 2026-10-21
completed:
---
# BL-3014 — Land the ported unit programs 1652, 2413, 3219 and 3302 with their Core, Engine and Libcurl fixes: fix the unit2413 ipv6 row and commit

## Goal

The ported unit programs for upstream cases 1652, 2413, 3219 and 3302, with the Core, Engine and Libcurl changes they needed, are committed with their tests passing.

## Context

Split from BL-2925 (the per-case findings are in the Notes of `Tasks/Deferred/BL-2925-*.md`: 1652's truncation at 2046 bytes in `DebugCallbackTransferEvents.ReportText`, 2413's new `ConnectionPeer`, 3219's `CURLINFO_PROTOCOL` mask `0x3fffffff` in `EasyInfoReaders.Longs.cs`, 3302 needing no fix). The work is written and building and sits in a stash that the dark factory applies for this task: stash `68641186c695a9c21837e1359a855737f82e4505` (the Engine cut and test, `ConnectionPeer` and its test, the Libcurl reader mask and test, the `InternalsVisibleTo` in `Libcurl/Libcurl.csproj`, the four ports `Unit1652InfoLineTruncation`, `Unit2413ConnectionPeer`, `Unit3219ProtocolMask` and `Unit3302GssApiDelegation` in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy/`, their registration in `UnitPrograms.Dns.cs`, and `UnitProgramsDnsEasyTests.cs`). Core, Engine and Libcurl fast tests passed. If the shift reports it did not apply, `git diff <sha>^1 <sha>` shows the tracked changes and `git show --stat <sha>^3` the added files.

Left in this task: one failing row of `Unit2413_PeerWrongInOneField_...` (the `ipv6=1` row) in `UnitProgramsDnsEasyTests.cs`. Read its actual stderr once and fix the expected count or first line, or the port if it is wrong against `tests/unit/unit2413.c` at tag `curl-8_22_0`. Then build, run the fast tests of the five test projects and commit. Do not run the cases through `CURL_UPSTREAM_CASE` or touch the ratchet (BL-3015), and do not run the quality measurement (BL-3016, BL-3017).

## Acceptance criteria

- [ ] The four programs are in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy/`, each doc comment naming the C rows it matches, registered in `UnitPrograms.Dns.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Dns/Easy/`.
- [ ] `UnitProgramsDnsEasyTests` passes in full, including the `ipv6=1` row of the 2413 test.
- [ ] Where a port found Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (`DebugCallbackTransferEventsTests`, `ConnectionPeerTests`, `EasyInfoReadersLongsTests`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Core.UnitTests`, `Curl.Engine.UnitTests`, `Libcurl.UnitTests` and `Curl.Conformance.UnitProgram.UnitTests`.

## Notes

- 2026-10-21: Split from BL-2925. The stash was re-stored under this task's ID (same commit 68641186c695a9c21837e1359a855737f82e4505) so the shift applies it on the first claim.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: d4cfaded 2ef82892 f6a8a38a 4789c509.
