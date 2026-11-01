---
id: GF-0089
title: --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported
area: options
platform: linux
key: options:http3-refused-by-openssl-build
severity: Medium
status: closed
scope: target
introduced-in: 7.66.0
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [options:--http3, options:--http3-only]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2332
tasks: [BL-2332]
---
# GF-0089 - --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported

## Summary

Curl differs from upstream curl in options on linux: --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported.

## Evidence

curl --http3: expected 'exit 2: curl: option --http3: the installed libcurl version does not support this | curl: try ...', actual 'exit 2: curl: (2) no URL specified | ...'. curl --http3-only: same pair with --http3-only. Reproduce on linux: curl --http3 and curl --http3-only (the items' evidence), or powershell -NoProfile -File Gap/Tools/Measure-OptionGap.ps1 -OutFile $env:TEMP/options.json. Both rows in CommandLineOptionTable.cs (lines 340-341) carry RefusedBySchannelBuild(), which refuses on Windows only, so Linux falls through to the normal path.

## Suggestion

Make the refusal follow the platform's reference build, not only the Schannel build. In Curl.Cli.UnitLibrary, change the http3 and http3-only rows of CommandLineOptionTable (and the CommandLineOption.RefusedBySchannelBuild flag, CommandLineRefusal.InstalledLibcurlDoesNotSupport) so the Linux and macOS builds answer 'option --http3: the installed libcurl version does not support this' (exit 2), as the reference built from the tarball with OpenSSL does (no ngtcp2/quiche). Measure real curl on each platform before pinning. Upstream: docs/cmdline-opts/http3.md and http3-only.md. The platform-specific cause is the reference build's HTTP/3 feature set. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Measurements

- 2026-10-15_0751: 2 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-options.
- 2026-10-15_0900: Filed BL-2332.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
