---
id: BL-2630
title: Make upstream cases 667 and 668 pass: MIME callback parts post their data in curl's chunks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2630 — Make upstream cases 667 and 668 pass: MIME callback parts post their data in curl's chunks

## Goal

Upstream cases 667 and 668 pass through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test667 (lib667, base64 part read one byte a call, unknown size): `<verify><protocol> differs at byte 302 (line 14): expected "4\r\n" ("ZHVt"), got "8\r\n" ("ZHVtbXk=")` - curl encodes and sends each callback read as it comes. test668 (lib668): `differs at byte 181 (line 8): expected "cd\r\n", got "63\r\n"` - the known-size callback part `field1` posts no data. Related: BL-2624 (645 chunk sizes).

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fault was in Curl.Core.UnitLibrary's multipart streams, not Libcurl or the engine; touches gained Curl.Core.UnitLibrary and Curl.Core.UnitTests (no other task in Doing named them).
- test668: a known-size callback part that had given all its bytes still counted as the fill's one callback read, so the fill stopped before field2's headers. ConcatenatedReadStream now counts bytes per callback segment and passes one at its known size without reading it (curl's read_part_content spares the read at offset >= datasize).
- test667: EncodedReadStream read the callback repeatedly in one fill, and Base64DataEncoding held output until a full 76-column line. An encoded callback segment now reads its source once per read (ReadsSourceOncePerRead); a read that gives nothing reports StoppedFilling, ending the fill, which restarts when it is empty (Curl_mime_read's loop on STOP_FILLING). Base64 now encodes each complete 3-byte group at once; the bytes for whole data are unchanged.
- Unit tests: ConcatenatedReadStreamTests (sized callback, empty sized callback, rewind, encoded one-byte callback sync and async) and the new Base64DataEncodingTests (ADR-0482).
- Platform: neither case's output depends on paths or OS text (668's file part sends only its base name).
- Measure-CodeQuality for Curl.Core.UnitLibrary: 100% line and branch; the one failing member (ConcatenatedReadStream ctor, complexity 14) was split into two static helpers.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream cases 667 and 668 pass: MIME callback parts post their data in curl's chunks
