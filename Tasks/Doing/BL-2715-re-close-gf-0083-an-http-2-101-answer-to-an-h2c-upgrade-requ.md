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
completed:
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

- [ ] `behaviour:test1704`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 06a09dd0 8b83e917 1fd5de8c 28346828.
