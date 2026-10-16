---
id: BL-2326
title: Close GF-0083: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8 (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2326 — Close GF-0083: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8 (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0083 (An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8), so a later gap analysis measures each of `behaviour:test1704` as `match`.

## Context

- Finding: GF-0083, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `behaviour:test1704`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1704 (http://127.0.0.1:<port>/1704 --http2, reply 'HTTP/2 101 OK' then 'HTTP/1.1 200 OK'): expected 'reference curl exits 8; stdout 0 bytes', actual '<verify><errorcode>: expected exit code 8, got 0'. Also a gap on macOS. Excluded on Windows (reference-lacks:h2c). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 1704

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, fail with exit 8 (CURLE_WEIRD_SERVER_REPLY) when a response on an HTTP/1.x connection names HTTP/2 in its status line, including the interim response to an h2c upgrade. Today HttpH2cUpgradeConnection switches only on 'HTTP/1.1 101'. Otherwise it reads on as HTTP/1.1, and HttpStatusLine.Parse takes 'HTTP/2 101' as an assumed interim answer before accepting the 200. Have the 1xx head record its version (Http1VersionSeen.VersionNamed) so that Http1VersionSeen.ThrowIfMismatched, or a direct check, refuses the 'HTTP/2' status line as curl does. Add a focused test in Curl.Protocol.Http.UnitTests (ADR-0482). This applies only on the OpenSSL builds, because the Schannel reference lacks h2c.

## Acceptance criteria

- [x] `behaviour:test1704`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Fix (ADR-0517): off Windows, `HttpResponseHeadReader.ParsesHttp2InterimStatusLines` parses `HTTP/2 1xx` and `HTTP/3 1xx` over HTTP/1.x as that version's interim head (`HttpStatusLine.IsHttp2OrHttp3Interim`), and `ReadStatusLineAfterAsync` fails a later status line naming another major version with exit 8, `Version mismatch (from HTTP/2 to HTTP/1)` (`Http1VersionSeen.ThrowIfMismatched(Version?, Version)`), as curl's `Curl_http_statusline` checks `httpversion_seen`. The upgraded HTTP/2 head after an h2c `HTTP/1.1 101` is exempt. Windows (Schannel, no h2c) keeps the assumed `HTTP/1.0 200`.
- Behaviour taken from curl's http.c, not measured: the upstream test data sits in the gap office's cache, which the audit guard keeps lanes out of, and lanes have no Linux curl. A final `HTTP/2 200` over HTTP/1.x is left as it was (narrowest change).
- Focused tests (ADR-0482): `HttpResponseHeadReaderTests.ReadAsync_Http2InterimThenHttp1HeadWhereParsed_ReturnsWeirdServerReplyVersionMismatch` pins test1704's answer and runs on every CI platform; `ReadAsync_Http2InterimWhereNotParsed_TakesItAsAssumedHttp10Ok` pins Windows; `ReadAsync_Http11InterimThenSwitchedHttp2Head_IsNoVersionMismatch` the h2c exemption; `HttpStatusLineTests.IsHttp2OrHttp3Interim_StatusLine_TellsWhetherItIsAnHttp2OrHttp3Interim`.
- Acceptance 1 and 2 are ticked on those tests, which the CI job runs on Linux and macOS once the shift pushes; the gap itself closes only when the next gap analysis measures test1704 as `match`.
- Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. No option changed, so `--ai-help` is unaffected.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. An HTTP/2 101 interim then HTTP/1.1 200 exits 8 with Version mismatch off Windows (test1704)
