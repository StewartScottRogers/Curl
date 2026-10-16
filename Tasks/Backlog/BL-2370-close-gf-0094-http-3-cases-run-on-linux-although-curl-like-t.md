---
id: BL-2370
title: Close GF-0094: HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2370 — Close GF-0094: HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0094 (HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there), so a later gap analysis measures each of `behaviour:test2500`, `behaviour:test2503` as `match`.

## Context

- Finding: GF-0094, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `behaviour:test2500`, `behaviour:test2503`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2500 (--http3 --resolve localhost:<port>:127.0.0.1 https://localhost:<port>/2500): expected 'upstream test2500 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET https://localhost:9019/2500 HTTP/1.1\r\n", got the end'. behaviour:test2503 (-w '%{header_json}\n' --http3-only ...): expected 'upstream test2503 passes', actual '<verify><stdout> differs at byte 0 (line 1): expected "HTTP/3 200 \r\n", got the end'. No request and no stdout: CommandLineOptionTable.cs:340-341 refuse --http3 and --http3-only with RefusedWithoutQuic on every reference build (BL-2332, GF-0089). The measurement's reference also has no QUIC library ('libcurl/8.22.0 OpenSSL/3.0.13 ... nghttp2/1.59.0', no ngtcp2 or nghttp3). The conformance harness still lists 'http/3' for UpstreamCurlPlatform.Unix and MacOS (UpstreamCurlPlatform.cs:71,74), so it runs these cases instead of skipping them. Windows skips them ('Curl lacks the feature http/3', excluded reference-lacks:http/3). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 2500 2503

Suggestion, copied from the finding:

In Curl.Conformance.UnitLibrary, remove 'http/3' from UpstreamCurlPlatform.Unix and UpstreamCurlPlatform.MacOS. Fix the class remarks that say Curl still serves --http3 off Windows. Upstream's runtests.pl then skips cases needing http/3 on a build without QUIC, as it does for the reference, and they are excluded as on Windows. As an alternative, derive the feature from CommandLineOptions.ActsAsQuicBuild so it follows the CLI's refusal. Update UpstreamCurlPlatformTests. The product needs no change: Curl already matches the reference here. Explained by ADR-0540 (refuse --http3 on every reference build); the harness's feature list predates it (BL-2261).

## Acceptance criteria

- [ ] `behaviour:test2500`: Curl answers what curl 8.22.0 answers on linux, `upstream test2500 passes`, so the item measures `match`.
- [ ] `behaviour:test2503`: Curl answers what curl 8.22.0 answers on linux, `upstream test2503 passes`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
