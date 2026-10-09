---
id: GF-0005
title: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials
area: behaviour
key: behaviour:auth-retry-not-sent-for-upload-or-form-body
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test154, behaviour:test155, behaviour:test258, behaviour:test259, behaviour:test1030, behaviour:test1071, behaviour:test1075]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1798
tasks: [BL-1798]
---
# GF-0005 - With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials

## Summary

Curl differs from upstream curl in behaviour: With --anyauth or --proxy-anyauth, a -T upload or -F form that draws a 401/407 is never resent with credentials.

## Evidence

Every item expects 'upstream test<N> passes'. test1030 actual: <verify><protocol> differs at byte 187 (line 11): expected 'PUT /1030 HTTP/1.1', got the end. test1071, test1075, test154 and test155 have the same shape. test259: expected 'Content-Disposition: form-data; name="name"', got the end. test258: expected 'POST http://remotehost:54321/we/want/258 HTTP/1.1', got the end. The reference curl exits 0 on test1075. test1030's 401 keeps the connection alive, so this is not the closed-connection cause. Cause in code: HttpProtocolHandler.RetryAuthorizationAsync answers only when MayRetry holds ('only when its body can be sent again'). The -T file body and the multipart body that were already sent in full are not treated as rewindable, so RetryOfAsync returns no retry. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1030,1075,259

## Suggestion

In Curl.Protocol.Http.UnitLibrary, make HttpProtocolHandler.MayRetry and the upload rewind (TryRewindUpload / HttpRequestBodyWriter) treat a -T file and a -F multipart body as rewindable. Then a 401 under --anyauth, or a 407 under --proxy-anyauth, resends the PUT or POST with the picked scheme's credentials and the whole body, as upstream tests/data/test1030 and test259 expect.

## Measurements

- 2026-10-08_1640: 7 of 7 items are gaps.
- 2026-10-08_2029: 5 of 7 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1798.
