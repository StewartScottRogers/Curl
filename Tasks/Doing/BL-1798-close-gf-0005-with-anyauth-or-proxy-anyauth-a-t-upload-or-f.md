---
id: BL-1798
title: Close GF-0005: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1798 — Close GF-0005: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0005 (With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials), so a later gap analysis measures each of `behaviour:test154`, `behaviour:test155`, `behaviour:test258`, `behaviour:test259`, `behaviour:test1030`, `behaviour:test1071`, `behaviour:test1075` as `match`.

## Context

- Finding: GF-0005, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test154`, `behaviour:test155`, `behaviour:test258`, `behaviour:test259`, `behaviour:test1030`, `behaviour:test1071`, `behaviour:test1075`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1030 actual: <verify><protocol> differs at byte 187 (line 11): expected 'PUT /1030 HTTP/1.1', got the end. test1071, test1075, test154 and test155 have the same shape. test259: expected 'Content-Disposition: form-data; name="name"', got the end. test258: expected 'POST http://remotehost:54321/we/want/258 HTTP/1.1', got the end. The reference curl exits 0 on test1075. test1030's 401 keeps the connection alive, so this is not the closed-connection cause. Cause in code: HttpProtocolHandler.RetryAuthorizationAsync answers only when MayRetry holds ('only when its body can be sent again'). The -T file body and the multipart body that were already sent in full are not treated as rewindable, so RetryOfAsync returns no retry. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1030,1075,259

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, make HttpProtocolHandler.MayRetry and the upload rewind (TryRewindUpload / HttpRequestBodyWriter) treat a -T file and a -F multipart body as rewindable. Then a 401 under --anyauth, or a 407 under --proxy-anyauth, resends the PUT or POST with the picked scheme's credentials and the whole body, as upstream tests/data/test1030 and test259 expect.

## Acceptance criteria

- [ ] `behaviour:test154`: Curl answers what curl 8.21.0 answers, `upstream test154 passes`, so the item measures `match`.
- [ ] `behaviour:test155`: Curl answers what curl 8.21.0 answers, `upstream test155 passes`, so the item measures `match`.
- [ ] `behaviour:test258`: Curl answers what curl 8.21.0 answers, `upstream test258 passes`, so the item measures `match`.
- [ ] `behaviour:test259`: Curl answers what curl 8.21.0 answers, `upstream test259 passes`, so the item measures `match`.
- [ ] `behaviour:test1030`: Curl answers what curl 8.21.0 answers, `upstream test1030 passes`, so the item measures `match`.
- [ ] `behaviour:test1071`: Curl answers what curl 8.21.0 answers, `upstream test1071 passes`, so the item measures `match`.
- [ ] `behaviour:test1075`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- `HttpProtocolHandler.MayRetry` now accepts a stream body that can seek, and
  `RewindForResend` seeks it back before a 401 or 407 retry (ADR-0440). Unseekable streams
  (stdin) keep ADR-0034's behaviour: the challenge is the result.
- Tests: `ExecuteAsync_ChallengeToASeekableStreamBody_SendsTheBodyAgain`,
  `ExecuteAsync_ChallengeToAnUnseekableStreamBody_ReturnsThe401`,
  `ExecuteAsync_407ToASeekableStreamBody_SendsTheBodyAgain`,
  `ExecuteAsync_407ToAnUnseekableStreamBody_ReturnsIt`.
- Not re-measured with `Gap/Tools/Measure-UpstreamCases.cs`: the audit guard refuses lanes
  any read of `Gap/`. The gap closes on the next gap analysis run (ADR-0433).
- No option changed, so `--ai-help` needs nothing.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
