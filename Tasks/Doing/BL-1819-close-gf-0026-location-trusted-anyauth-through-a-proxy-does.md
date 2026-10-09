---
id: BL-1819
title: Close GF-0026: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
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

- [ ] `behaviour:test1088`: Curl answers what curl 8.21.0 answers, `upstream test1088 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
