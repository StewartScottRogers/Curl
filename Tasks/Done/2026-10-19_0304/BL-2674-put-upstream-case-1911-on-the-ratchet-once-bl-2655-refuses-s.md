---
id: BL-2674
title: Put upstream case 1911 on the ratchet once BL-2655 refuses strings past CURL_MAX_INPUT_LENGTH
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2655]
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2674 — Put upstream case 1911 on the ratchet once BL-2655 refuses strings past CURL_MAX_INPUT_LENGTH

## Goal

Upstream case 1911 passes and is on the ratchet.

## Context

Found by BL-2545 (lib1911 port). lib1911 sets every string option but four to a string of 8000001 `A`s and expects each to be refused (43, 48, 4 or 1). Curl's Libcurl takes them, so the program ends with `TEST_ERR_FAILURE`: `<verify><errorcode>`: expected exit code 0, got 44. BL-2655 adds the `CURL_MAX_INPUT_LENGTH` refusal; once it lands, run case 1911 (`CURL_UPSTREAM_CASE=1911`) and fix any string option the program's stderr still names as answering something else.

## Acceptance criteria

- [x] No string option lib1911 sets answers other than 43, 48, 4 or 1 for a string past `CURL_MAX_INPUT_LENGTH`, with a focused unit test in `Libcurl.UnitTests` for any option fixed here.
- [x] Case 1911 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

BL-2655 had already landed: case 1911 passed through the runner on Windows with no product change, so no string option needed a fix or a new unit test. Added 1911 to PassingUpstreamCases.txt. The case reads no file system path or OS error text, so it is platform-neutral.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e34a74aa 19b5fc62 4272a6b9.
- 2026-10-19: Doing -> Done. Case 1911 already passed after BL-2655; listed on the ratchet
