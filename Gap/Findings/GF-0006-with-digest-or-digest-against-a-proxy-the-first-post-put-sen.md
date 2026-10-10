---
id: GF-0006
title: With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0
area: behaviour
key: behaviour:auth-probe-request-sends-body
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test88, behaviour:test175, behaviour:test177, behaviour:test245, behaviour:test246, behaviour:test1001, behaviour:test1002, behaviour:test1284, behaviour:test1285, behaviour:test2058, behaviour:test2059, behaviour:test2060, behaviour:test2067, behaviour:test2068, behaviour:test2069]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2002
tasks: [BL-1799, BL-2002]
---
# GF-0006 - With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0

## Summary

Curl differs from upstream curl in behaviour: With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0.

## Evidence

Every item expects 'upstream test<N> passes'. test175 actual: <verify><protocol> differs at byte 96 (line 5): expected 'Content-Length: 0', got 'Content-Length: 11'. test177, test245, test246, test1284 and test2067-2069 have the same shape. test88/test1285: expected 'Content-Length: 0', got 'Content-Length: 85'. test1001/1002/2058-2060: expected 'Content-Length: 0', got 'Content-Length: 3'. The reference curl exits 0 on test177. Cause in code: HttpProtocolHandler frames the first request with its full body even when the authenticator has picked Digest and holds no challenge yet. curl sends that probe with an empty body and Content-Length: 0, overriding a user Content-Length (test1284), and sends the body only on the authenticated request. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 175,88,1001

## Suggestion

In Curl.Protocol.Http.UnitLibrary, when --digest is chosen for the origin or the proxy and no challenge has been answered yet, frame the first POST or PUT (HttpRequestFraming / HttpRequestBodyWriter) with an empty body and Content-Length: 0, replacing a user-supplied Content-Length. Send the real body on the retry that carries Authorization. Leave the body in place when the server answers the probe without a challenge (test175 then sends it on the next request, as upstream's <verify> shows).

## Measurements

- 2026-10-08_1640: 15 of 15 items are gaps.
- 2026-10-08_2029: 5 of 15 items are gaps.
- 2026-10-10_0657: 5 of 15 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1799.
- 2026-10-10_0756: Filed BL-2002.
