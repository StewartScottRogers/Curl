---
id: GF-0099
title: HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl
area: options
platform: windows
key: options:http2-options-unsupported-answer
severity: Medium
status: closed
scope: target
introduced-in: 7.33.0
opened: 2026-10-23_1637
closed: 2026-10-25_1542
regression: false
items: [options:--http2, options:--http2-prior-knowledge, options:--proxy-http2, options:--proxy-http2:no-form]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-3432
tasks: [BL-3432]
---
# GF-0099 - HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl

## Summary

Curl differs from upstream curl in options: HTTP/2 options are accepted by Curl but refused by the reference curl as unsupported by its libcurl.

## Evidence

curl --http2: expected 'exit 2: curl: option --http2: the installed libcurl version does not support this' (plus the 'try curl --help' line), actual 'exit 2: curl: (2) no URL specified' (plus the same help line). The same pattern holds for --http2-prior-knowledge, --proxy-http2 and --no-proxy-http2 (expected text names the option as --no-proxy-http2). Reproduce: powershell -NoProfile -File Gap\Tools\Invoke-GapProbe.ps1 with the command line 'curl --http2', or the whole tool: powershell -NoProfile -File Gap\Tools\Measure-OptionGap.ps1 -OutFile $env:TEMP\options.json

## Suggestion

The Windows reference curl (Git for Windows, Schannel) was built without HTTP/2, so it refuses these options. Curl implements HTTP/2 and accepts them. The gap is a reference-build difference, not a missing option: the yardstick is upstream's release, where a build with nghttp2 accepts these options. Close it by one of two routes. (a) Re-measure these facets against a reference curl built with HTTP/2, and mark them match or excluded with a reason. (b) Add a build-feature switch in Curl.Cli.UnitLibrary (the CommandLineParser branch that handles --http2, --http2-prior-knowledge and --proxy-http2, with their entries in CommandLineOptionTable) so a Curl build without HTTP/2 prints 'option --X: the installed libcurl version does not support this' (see docs/cmdline-opts/http2.md, http2-prior-knowledge.md and proxy-http2.md). Route (a) is likely right, because Curl always carries HTTP/2. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Measurements

- 2026-10-23_1637: 4 of 4 items are gaps.
- 2026-10-25_1542: 0 of 4 items are gaps.
- 2026-10-30_2103: 0 of 4 items are gaps.

## Log

- 2026-10-23_1637: Opened by gap-options.
- 2026-10-23_1729: Filed BL-3432.
- 2026-10-23_2330: Stewart decided 'Keep HTTP/2 on': ADR-0786 stands, so the Windows build keeps HTTP/2 and Curl will not refuse it to match the Schannel reference. Gap/Baselines/platform-exclusions.json now has Measure-GapAreas.ps1 measure options:--http2, options:--http2-prior-knowledge, options:--proxy-http2 and options:--proxy-http2:no-form as excluded with reason decided:ADR-0786 on Windows only; this finding closes on the next run that measures them so (findings close only on measurement).
- 2026-10-25_1542: Closed: run 2026-10-25_1542 measured every item as match or excluded on windows.
