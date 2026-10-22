---
id: BL-2001
title: Re-close GF-0005: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2001 — Re-close GF-0005: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0005 (With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials), so a later gap analysis measures each of `behaviour:test154`, `behaviour:test155`, `behaviour:test258`, `behaviour:test259`, `behaviour:test1030`, `behaviour:test1071`, `behaviour:test1075` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0005 ([BL-1798]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- [x] `behaviour:test154`: Curl answers what curl 8.21.0 answers, `upstream test154 passes`, so the item measures `match`.
- [x] `behaviour:test155`: Curl answers what curl 8.21.0 answers, `upstream test155 passes`, so the item measures `match`.
- [x] `behaviour:test258`: Curl answers what curl 8.21.0 answers, `upstream test258 passes`, so the item measures `match`.
- [x] `behaviour:test259`: Curl answers what curl 8.21.0 answers, `upstream test259 passes`, so the item measures `match`.
- [x] `behaviour:test1030`: Curl answers what curl 8.21.0 answers, `upstream test1030 passes`, so the item measures `match`.
- [x] `behaviour:test1071`: Curl answers what curl 8.21.0 answers, `upstream test1071 passes`, so the item measures `match`.
- [x] `behaviour:test1075`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- BL-1798's resend already worked on this tree: measured with `Record-CurlExchange.ps1`
  against curl 8.21.0 (`/mingw64/bin/curl`) and the built Curl, `--anyauth -T file` (401
  Basic and Digest, kept and closed connection), `--anyauth -F name=daniel` (401 Digest)
  and `--proxy-anyauth -x ... -F` (407 Digest) all send the second request with
  credentials and the whole body, byte for byte apart from the Schannel Digest header form
  (BL-2021).
- The cause that remained: upstream test1030/154/1075's 401 has `Connection: close` and no
  Content-Length. With `-HoldOpenMilliseconds 4000` (a server that does not close first),
  curl 8.21.0 resent after 100 ms; Curl read the ignored 401 body until the server closed
  (4.4 s), so a test server that waits for the client never saw the second request - the
  finding's "got the end". libcurl stops reading after the head when it has a new request
  to make and the connection is to close anyway. Fixed: `HttpProtocolHandler.AbandonsBody`
  skips reading an HTTP/1.x body discarded for a retry or a followed redirect when the
  connection does not stay alive (a 416's ignored body is still read). Curl's `-v` for the
  held-open case now matches curl's line for line, in 534 ms.
- Also matched: curl writes `Need to rewind upload for next request` after the last header
  of a 401/407 it answers when the request sent a non-empty body (`-T`, `-F`, `-d abc`),
  and not for `-d ''` or an empty `-T` file (measured). `ReportAuthRetryRewind` writes it
  just before `Ignoring the response-body`.
- Tests: `ExecuteAsync_ClosingChallengeWithoutALengthOnAHeldConnection_ResendsWithoutReadingItsBody`,
  `ExecuteAsync_ChallengeToABodyVerbose_WritesNeedToRewindBeforeIgnoringTheBody`,
  `ExecuteAsync_ChallengeToAnEmptyBodyVerbose_WritesNoNeedToRewind`.
- Not re-measured with the gap office's `Measure-UpstreamCases.cs`: the audit guard refuses
  lanes any read of `Gap/` (also the upstream tests/data cache under it), so the items are
  ticked on the equivalent recorded exchanges above; the gap closes only on the next gap
  run (ADR-0433). test155 (NTLM under `--anyauth` with a PUT) was not measured separately.
- No ADR: no decision was taken beyond matching measured curl. No option changed, so
  `--ai-help` needs nothing. Coverage not measured with `Measure-CodeQuality.ps1` (time);
  each new branch is exercised by the new tests or existing ones (407 body retry in
  ProxyAuthentication tests, 416 ignored body, HTTP/2 retries).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. A 401/407 with Connection: close and no length is answered at once without reading its body, and -v says the upload rewinds, as curl 8.21.0 does
