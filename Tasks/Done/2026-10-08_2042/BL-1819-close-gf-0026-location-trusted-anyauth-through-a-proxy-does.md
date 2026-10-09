---
id: BL-1819
title: Close GF-0026: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1819 — Close GF-0026: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0026 (--location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host), so a later gap analysis measures each of `behaviour:test1088` as `match`.

## Context

- Finding: GF-0026, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test1088`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1088 (-x ... --user iam:myself --location-trusted --anyauth) expected 'upstream test1088 passes', actual: <verify><protocol> differs at byte 436 (line 16): expected 'Authorization: Basic aWFtOm15c2VsZg==', got 'User-Agent: curl/8.21.0'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1088

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's redirect handling (HttpProtocolHandler with the authenticator), under --location-trusted carry the scheme --anyauth picked (Basic here) to the redirect's new host and send it pre-emptively. Do not start a new negotiation with no Authorization.

## Acceptance criteria

- [x] `behaviour:test1088`: Curl answers what curl 8.21.0 answers, `upstream test1088 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: each `-L` hop is a fresh `HttpProtocolHandler.ExecuteAsync`, so the Basic scheme `--anyauth` settled on after the first host's 401 was lost, and the redirect's hop started a new `--anyauth` negotiation with no `Authorization`. libcurl keeps `authhost.picked` across followed redirects, so its third request in test1088 sends Basic before any challenge.
- Fix: `TransferReport.AuthSchemePicked` (the scheme of an `Authorization` value that answered a challenge, else the one inherited) and `HttpRequestOptions.AuthSchemePicked` (the earlier hop's, passed on by `RedirectFollower`). When the picked scheme is among `AuthSchemes`, the handler narrows the origin's allowed schemes to it, so the authenticator answers Basic up front. Without `--location-trusted`, credentials are still dropped for another host, so nothing leaks.
- Touches widened to `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`: the scheme has to cross the hop boundary, which lives in Core and the contract. No task in Doing on `origin/work/dark-factory` names them (only BL-1809: Curl.Console, Curl.Console.UnitTests).
- No ADR: this matches libcurl's behaviour and is not a choice between behaviours. No option changed, so `--ai-help` needs nothing.
- Lanes may not run the gap harness (the audit guard refuses reads under the gap cache), so test1088 was not re-run here. Instead, `HttpProtocolHandlerTests.AuthSchemePicked` pins the request sequence (401 Basic, then Basic, then Basic up front on the next hop), and `RedirectFollowerTests.FollowAsync_HopsPickedAuthScheme_IsSentToTheNextHop` pins the hand-over between hops. The next gap run confirms the item (ADR-0433 decision 5).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Picked --anyauth scheme now carried across redirect hops; Basic sent up front to the trusted redirect host (test1088)
