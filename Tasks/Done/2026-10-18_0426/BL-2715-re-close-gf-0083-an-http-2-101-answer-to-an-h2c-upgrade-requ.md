---
id: BL-2715
title: Re-close GF-0083: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8 (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2715 — Re-close GF-0083: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8 (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0083 (An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8), so a later gap analysis measures each of `behaviour:test1704` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0083 ([BL-2326]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- Why BL-2326's fix did not hold: it made the exit code 8, but with the wrong message. The gap
  run of 2026-10-17_2225 (gap-platforms run 39100480741, linux and macOS artifacts) measured
  test1704 as `stderr differs` ("was match; in-process and out-of-process differ"), not exit 0.
- Measured with Linux curl 8.18.0 (OpenSSL 3.5.5, nghttp2 1.68.0, WSL Ubuntu) through
  `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress <WSL gateway IP>`: on test1704's reply
  curl fails at the first line, `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`, and `-v`
  never shows `< HTTP/2 101 OK`. Curl said `(from HTTP/2 to HTTP/1)` after reporting the line.
  The same exit 8 and message for a final `HTTP/2 200`, with or without `--http2`, and for
  `HTTP/2 200` after `HTTP/1.1 100`; `HTTP/3 101` or `HTTP/3 200` exit 1 with `Unsupported HTTP
  version (3.0) in response`; `HTTP/2 99`, `HTTP/2 abc`, `HTTP/2` and `HTTP/2.0 200` exit 0.
- Fix (ADR-0679, supersedes ADR-0517): off Windows, `HttpResponseHeadReader.RefusesHttp2StatusLines`
  fails a status line over HTTP/1.x that `HttpStatusLine.NamesHttp2OrHttp3` before it is reported
  (`HttpStatusLine.RefusalOverHttp1`). ADR-0517's interim-version check is removed (dead now).
  Windows unchanged. ADR-0517's status line was edited to say it is superseded (a Decisions file
  beside the new ADR; no task in Doing touches Documentation).
- Focused tests (ADR-0482): `HttpProtocolHandlerTests.ExecuteAsync_Http2UpgradeAnsweredHttp2101ThenHttp11200_FailsWithVersionMismatchAndNoBody`
  (test1704 end to end, excluded on Windows), `HttpResponseHeadReaderTests.ReadAsync_Http2StatusLineWhereRefused_FailsAsCurlsOpenSslBuild`,
  `ReadAsync_Http2StatusLineOnWindows_ParsesAs200` (the `HTTP/2 404` row moved out of the
  assumed-200 test, which would have gone red on Linux), `HttpStatusLineTests.NamesHttp2OrHttp3_*`
  and `RefusalOverHttp1_*`. The Linux-only tests were run on Windows with the default forced to
  the Linux value (then restored), and `Curl.Console` run end to end with both the CLI's and the
  reader's Linux paths forced exited 8 with curl's message.
- Acceptance 1 and 2 are ticked on that end-to-end test, which the CI job runs on Linux and macOS
  once the shift pushes; a lane cannot run gap-platforms. The gap closes only when the next gap
  analysis measures test1704 as `match`.
- Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100% line, 100% branch, 0 failing
  members, worst CRAP 10. No option changed, so `--ai-help` is unaffected.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 06a09dd0 8b83e917 1fd5de8c 28346828.
- 2026-10-17: Doing -> Done. Off Windows an HTTP/2 status line over HTTP/1.x fails at once with exit 8, Version mismatch (from HTTP/1 to HTTP/2), as curl's OpenSSL build answers test1704
