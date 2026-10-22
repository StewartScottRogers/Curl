---
id: BL-3029
title: Shuffle a direct https:// origin's DoH addresses under CURLOPT_DNS_SHUFFLE_ADDRESSES again
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/ShufflingDnsResolver.cs, Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3029 — Shuffle a direct https:// origin's DoH addresses under CURLOPT_DNS_SHUFFLE_ADDRESSES again

## Goal

With `--dns-shuffle-addresses` (`CURLOPT_DNS_SHUFFLE_ADDRESSES`) and `--doh-url`, a direct `https://` connect dials the DoH answer's addresses in shuffled order again, while still sending the HTTPS-RR query first.

## Context

- Found by the shift-end review (ADR-0710) of `2b068e0a3...167e765b2`. All three reviewers flagged it: pr-review-toolkit code-reviewer, silent-failure-hunter and type-design-analyzer.
- `Curl.Networking.UnitLibrary/TcpConnector.cs:1238` adds `KindOfResolver`, which unwraps `ShufflingDnsResolver.Inner`.
- `HttpsOriginResolverFor` (`:1242`) now returns the inner `DohDnsResolver` for a shuffled DoH resolver.
- `LookUpAsync` (`:1224-1229`) then returns the result of `ResolveHttpsOriginAsync` as it is. The wrapper's shuffle never runs, and nothing shuffles the addresses later.
- Before this diff, the shuffled resolver was not an `IHttpsOriginDnsResolver`, so its addresses were shuffled (though no HTTPS-RR was sent). The change fixed the HTTPS-RR but dropped the shuffle, with no message.
- curl 8.22.0 shuffles every resolved address list, whichever resolver produced it.
- The new test `TcpConnectorShuffledResolverTests.ConnectAsync_ToAnHttpsOriginThroughAShuffledDohResolver_SendsTheHttpsRecordQuery` checks only that the query is sent, never the dial order.
- Preferred fix: make `ShufflingDnsResolver` pass the HTTPS-origin call through and shuffle its result, so the shuffle stays inside the wrapper. The other option is to shuffle on the HTTPS-origin branch in `TcpConnector`.

## Acceptance criteria

- [ ] A new test in `Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs` meets these conditions:
  - Setup: a `ShufflingDnsResolver` over a DoH resolver with a fixed `randomBelow`, and a direct `https://` target.
  - It asserts that the addresses are dialled in the shuffled order, not the DoH answer's order.
  - It asserts that the HTTPS-RR query is still sent.
- [ ] The existing test `ConnectAsync_ToAnHttpsOriginThroughAShuffledDohResolver_SendsTheHttpsRecordQuery` still passes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage, and no method goes over complexity 10.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 217724c8 61a6aa9d 44447f62 d51ffe83.
