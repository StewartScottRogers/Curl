---
id: BL-4313
title: Fix flaky CoAP DTLS test LostServerFinishedFlight under a loaded full run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Coap.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4313 — Fix flaky CoAP DTLS test LostServerFinishedFlight under a loaded full run

## Goal

`RunAsync_Rfc6347Section4_2_4_LostServerFinishedFlight_ResendsTheKeyExchangeFlightAndCompletes` passes in every full fast run, not only alone.

## Context

Found by BL-4136 on 2026-10-29: it failed once in a full `dotnet test --filter "TestCategory!=Integration"` (257 ms, other lanes building) and passed when run alone. Probably a retransmission timer or a race on a real clock rather than a waited-for event (CLAUDE.md: wait for the event, never rely on scheduling).

## Acceptance criteria

- [x] The test waits for the events it needs (a fake TimeProvider or a signalled flight) instead of wall-clock timing.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: ImmediateTimeProvider fired every retransmission timer at once, racing the client reading the server reply. Added StarvedChannelTimeProvider (fires only when the test channel has nothing left to hand the client) and used it in the lost-flight tests; no wall-clock timing.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 00ae00f6 3d836d5e.
- 2026-10-29: Doing -> Done. Lost-flight DTLS tests retransmit only once the channel is starved, no wall-clock race
