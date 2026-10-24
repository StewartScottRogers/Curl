---
id: BL-3432
title: Close GF-0099: HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3432 — Close GF-0099: HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0099 (HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl), so a later gap analysis measures each of `options:--http2`, `options:--http2-prior-knowledge`, `options:--proxy-http2`, `options:--proxy-http2:no-form` as `match`.

## Context

- Finding: GF-0099, filed by the gap analysis office (ADR-0433).
- Area: options. Severity: Medium. Introduced in: 7.33.0.
- Platform: windows, measured against the Schannel curl of Git for Windows.
- Items: `options:--http2`, `options:--http2-prior-knowledge`, `options:--proxy-http2`, `options:--proxy-http2:no-form`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/*.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

curl --http2: expected 'exit 2: curl: option --http2: the installed libcurl version does not support this' (plus the 'try curl --help' line), actual 'exit 2: curl: (2) no URL specified' (plus the same help line). The same pattern holds for --http2-prior-knowledge, --proxy-http2 and --no-proxy-http2 (expected text names the option as --no-proxy-http2). Reproduce: powershell -NoProfile -File Gap\Tools\Invoke-GapProbe.ps1 with the command line 'curl --http2', or the whole tool: powershell -NoProfile -File Gap\Tools\Measure-OptionGap.ps1 -OutFile $env:TEMP\options.json

Suggestion, copied from the finding:

The Windows reference curl (Git for Windows, Schannel) was built without HTTP/2, so it refuses these options. Curl implements HTTP/2 and accepts them. The gap is a reference-build difference, not a missing option: the yardstick is upstream's release, where a build with nghttp2 accepts these options. Close it by one of two routes. (a) Re-measure these facets against a reference curl built with HTTP/2, and mark them match or excluded with a reason. (b) Add a build-feature switch in Curl.Cli.UnitLibrary (the CommandLineParser branch that handles --http2, --http2-prior-knowledge and --proxy-http2, with their entries in CommandLineOptionTable) so a Curl build without HTTP/2 prints 'option --X: the installed libcurl version does not support this' (see docs/cmdline-opts/http2.md, http2-prior-knowledge.md and proxy-http2.md). Route (a) is likely right, because Curl always carries HTTP/2. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Acceptance criteria

- [ ] `options:--http2`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --http2: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--http2-prior-knowledge`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --http2-prior-knowledge: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--proxy-http2`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --proxy-http2: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--proxy-http2:no-form`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --no-proxy-http2: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-23: Created.
