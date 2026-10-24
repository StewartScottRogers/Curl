---
id: BL-3430
title: Re-close GF-0031: Curl lists HTTP2 in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3430 — Re-close GF-0031: Curl lists HTTP2 in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0031 (Curl lists HTTP2 in Features; the Windows reference build does not), so a later gap analysis measures each of `features:HTTP2` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0031 ([BL-1824]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0031, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Platform: windows, measured against the Schannel curl of Git for Windows.
- Items: `features:HTTP2`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:HTTP2 expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks HTTP2; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop HTTP2 from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0141: HTTP/2 is hand-built and accepted everywhere, but offered by default only off Windows, so the Windows build does not offer it by default.

## Acceptance criteria

- [x] `features:HTTP2`: Curl answers what curl 8.22.0 answers, `not listed`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-23 (lane 4): No code change needed. The fix had already landed after the gap analysis measured: e44d1aaf5 (AF-0382, ADR-0808) dropped HTTP2 and HTTPSRR from `CurlVersionText.WindowsFeaturesLine`, and `CurlVersionTextTests` pins the Windows line without HTTP2. Checked: `curl -V` from Curl.Console on Windows prints `Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd` (no HTTP2). `dotnet build -warnaserror` is clean; fast tests are green (46 projects, 42913 passed, 0 failed). No option was added or changed, so `--ai-help` stays as it is. Off Windows, HTTP2 stays listed, as the OpenSSL reference lists it. No production code changed, so no quality run was needed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9f6e160d 1fd5de8c 28346828.
- 2026-10-23: Doing -> Done. Windows curl -V Features line omits HTTP2 as the Schannel reference does (already fixed by e44d1aaf5, verified)
