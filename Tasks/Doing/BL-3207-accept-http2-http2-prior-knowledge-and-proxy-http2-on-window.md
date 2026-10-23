---
id: BL-3207
title: Accept --http2, --http2-prior-knowledge and --proxy-http2 on Windows and list HTTP2 in curl -V
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Engine.UnitLibrary/ManagedCurlVersion.cs, Libcurl/Misc/Version/CurlVersionFeatures.cs, Curl.Cli.UnitTests/Http2OnWindowsTests.cs, Documentation/Planning/Decisions/README.md]
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

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6c603fd4 6eb56f1e 7ce332a1 5b6db80a 3639a5e6.
