---
id: BL-2332
title: Close GF-0089: --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2332 — Close GF-0089: --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0089 (--http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported), so a later gap analysis measures each of `options:--http3`, `options:--http3-only` as `match`.

## Context

- Finding: GF-0089, filed by the gap analysis office (ADR-0433).
- Area: options. Severity: Medium. Introduced in: 7.66.0.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `options:--http3`, `options:--http3-only`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/*.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

curl --http3: expected 'exit 2: curl: option --http3: the installed libcurl version does not support this | curl: try ...', actual 'exit 2: curl: (2) no URL specified | ...'. curl --http3-only: same pair with --http3-only. Reproduce on linux: curl --http3 and curl --http3-only (the items' evidence), or powershell -NoProfile -File Gap/Tools/Measure-OptionGap.ps1 -OutFile $env:TEMP/options.json. Both rows in CommandLineOptionTable.cs (lines 340-341) carry RefusedBySchannelBuild(), which refuses on Windows only, so Linux falls through to the normal path.

Suggestion, copied from the finding:

Make the refusal follow the platform's reference build, not only the Schannel build. In Curl.Cli.UnitLibrary, change the http3 and http3-only rows of CommandLineOptionTable (and the CommandLineOption.RefusedBySchannelBuild flag, CommandLineRefusal.InstalledLibcurlDoesNotSupport) so the Linux and macOS builds answer 'option --http3: the installed libcurl version does not support this' (exit 2), as the reference built from the tarball with OpenSSL does (no ngtcp2/quiche). Measure real curl on each platform before pinning. Upstream: docs/cmdline-opts/http3.md and http3-only.md. The platform-specific cause is the reference build's HTTP/3 feature set. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Acceptance criteria

- [ ] `options:--http3`: Curl answers what curl 8.22.0 answers on linux, `exit 2: curl: option --http3: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--http3-only`: Curl answers what curl 8.22.0 answers on linux, `exit 2: curl: option --http3-only: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
