---
id: BL-3207
title: Accept --http2, --http2-prior-knowledge and --proxy-http2 on Windows and list HTTP2 in curl -V
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3209]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Engine.UnitLibrary/ManagedCurlVersion.cs, Libcurl/Misc/Version/CurlVersionFeatures.cs, Curl.Cli.UnitTests/Http2OnWindowsTests.cs, Curl.Cli.UnitTests/CommandLineSchannelBuildRefusalTests.cs, Curl.Cli.UnitTests/CurlVersionTextTests.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Console.UnitTests/CurlCommandRunnerSchannelBuildRefusalTests.cs, Curl.Engine.UnitTests/ManagedCurlVersionTests.cs, Libcurl.UnitTests/Misc/Version/CurlVersionTests.cs, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3207 — Accept --http2, --http2-prior-knowledge and --proxy-http2 on Windows and list HTTP2 in curl -V

## Goal

On Windows, as already on Linux and macOS, `--http2`, `--http2-prior-knowledge` and `--proxy-http2` run over Curl's hand-built HTTP/2 (ADR-0141), and `curl -V` and `curl_version_info` list `HTTP2` (and `nghttp2`-equivalent version text only if upstream's line format needs it).

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 31, Stewart: "do it"). The engine already speaks HTTP/2 (through Libcurl HttpVersion=3 sends `Upgrade: h2c`, HttpVersion=5 sends the connection preface), but the Windows CLI refuses all three options with exit 2 "the installed libcurl version does not support this" (`RefusedBySchannelBuild`, ADR-0397), matching System32 curl 8.21.0 Schannel, which has no HTTP/2 (measured 2026-10-22). Stewart decided on 2026-10-22 to turn HTTP/2 on: write a new ADR recording his decision, that it supersedes ADR-0397's HTTP/2 part (`--ssl-sessions` stays refused), and the drop-in trade-off (a Windows script that relies on the refusal sees a transfer instead). Keep `-V` on Windows otherwise identical to System32 curl's line. Conformance: compare `--http2` and `--http2-prior-knowledge` behaviour (request bytes, `-w %{http_version}`, `-v` lines, exit codes, h2c upgrade refusal, ALPN fallback to 1.1) against an upstream curl 8.22.0 built with nghttp2 (the OpenSSL build gap-platforms.yml builds, or Git for Windows' curl) and pin any Windows-only differences. Keep `--ai-help` and `--help` right for the three options.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests/Http2OnWindowsTests.cs` (Windows `[OSCondition]`) shows the three options parse and select HTTP/2 instead of exit 2.
- [ ] `curl -V` on Windows lists `HTTP2` in Features in upstream's position, and `curl_version_info` sets CURL_VERSION_HTTP2; a test pins each.
- [ ] A conformance test or recorded comparison against an nghttp2-built curl 8.22.0 covers `--http2` over h2c and TLS, `--http2-prior-knowledge`, and ALPN fallback, with matching request bytes, `%{http_version}` and exit codes.
- [ ] The new ADR records Stewart's decision, supersedes ADR-0397's HTTP/2 part, and is in the ADR index; `--ai-help` and `--help` entries for the three options are right.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- 2026-10-22 (lane 7, first pass, no code edited): the conformance criterion cannot be met yet. `Record-CurlExchange.ps1` has an HTTP/2 proxy (`-Http2`) but no HTTP/2 origin, and no nghttp2-built curl 8.22.0 is on the machine (`/ucrt64/bin/curl` is 8.22.0 Schannel without nghttp2; WinGet's and WSL Ubuntu's curl are 8.18.0 with nghttp2). Filed BL-3209 for the recorder's HTTP/2 origin and the recordings; requeued behind it.
- Plan for the next claim: drop `.RefusedBySchannelBuild()` from the `http2`, `http2-prior-knowledge` and `proxy-http2` rows in `CommandLineOptionTable.cs` (keep it on `ssl-sessions`); fix the doc comments in `CommandLineOption.cs`/`CommandLineOptions.cs` that name HTTP/2; add `HTTP2` after `HSTS` to `CurlVersionText.WindowsFeaturesLine` and `ManagedCurlVersion.WindowsFeatures` (`Libcurl/Misc/Version/CurlVersion.cs` already maps the name to `CurlVersionFeatures.Http2`, so `CurlVersionFeatures.cs` likely needs no change); `--ai-help` follows the row's `RefusedByWindowsSchannelBuild` automatically. Keep the Windows version line without `nghttp2/…` text, as the task says (System32's line has none).
- Touches widened (no Doing task on origin/work/dark-factory names any of them): the existing tests that pin the Windows refusal and the Windows features line (`CommandLineSchannelBuildRefusalTests.cs`, `CurlVersionTextTests.cs`, `CurlCommandRunnerSchannelBuildRefusalTests.cs`, `ManagedCurlVersionTests.cs`, `Libcurl.UnitTests/.../CurlVersionTests.cs`), `CurlAiHelpText.cs` (its Schannel refusal line), and `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs`, whose remarks say `http/2` and `h2c` are off on Windows (BL-2230): decide there whether to turn them on (it changes which upstream cases run on Windows) or file that as follow-up.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6c603fd4 6eb56f1e 7ce332a1 5b6db80a 3639a5e6.
- 2026-10-22: Doing -> Backlog. Waits on BL-3209: Record-CurlExchange.ps1 has no HTTP/2 origin and no nghttp2-built curl 8.22.0 is recorded, so the conformance criterion cannot be checked
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6c603fd4 6eb56f1e 7ce332a1 5b6db80a 3639a5e6.
