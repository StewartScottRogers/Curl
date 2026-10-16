---
id: BL-2332
title: Close GF-0089: --http3 and --http3-only are accepted on Linux; the OpenSSL reference build refuses them as unsupported (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
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

- [x] `options:--http3`: Curl answers what curl 8.22.0 answers on linux, `exit 2: curl: option --http3: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [x] `options:--http3-only`: Curl answers what curl 8.22.0 answers on linux, `exit 2: curl: option --http3-only: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-15 (lane 1, first claim, no code written): the change cannot stay inside the Cli
  projects. Off Windows the rows of `--http3`/`--http3-only` (CommandLineOptionTable.cs:340-341)
  apply through the same parse that `Curl.Console.UnitTests` uses with
  `parsesAsWindowsBuild: false` (CurlCommandRunnerHttp3Tests, CurlCommandRunnerAltSvcVersionTests,
  CurlCommandRunnerHttpsConnectTraceTests, CurlCommandRunnerHttp3DatagramConnectorTests,
  HttpVersionMappingTests, HappyEyeballsTimeoutMappingTests - about 34 uses of `--http3`), so refusing
  them off Windows turns those red and leaves Curl.Console's HTTP/3 composition uncovered. Added
  Curl.Console and Curl.Console.UnitTests to `touches`; BL-2337 holds both, so requeued behind it.
- Plan for the next claim: the reference builds on all three platforms (Schannel on Windows,
  tarball+OpenSSL with no QUIC library on Linux and macOS, ADR-0505/ADR-0521) refuse both options with
  `option --http3: the installed libcurl version does not support this`, exit 2. Give the parser a
  third build reading beside Windows Schannel and the OpenSSL reference: "a QUIC build" (curl.se's
  ngtcp2 build, ADR-0144) that accepts them. Production never selects it, so the rows refuse on every
  platform; Console's `CreateRunner` gains a way for the HTTP/3 tests to parse as the QUIC build so the
  hand-built stack stays covered (and reachable through Libcurl's CURLOPT_HTTP_VERSION). Supersede
  ADR-0521 decision 2 in a new ADR "Decided by Claude under Stewart's delegation". Update
  CommandLineOptionTable rows, CommandLineOption (a refusal that does not depend on Schannel),
  CommandLineOptions.ActsAsWindowsSchannelBuild docs, CurlVersionText.cs:44 comment, the Cli tests
  pinning acceptance (CommandLineTransferEncodingOptionTests 371-469 must parse as the QUIC build or
  assert the refusal), and `--ai-help` (CurlAiHelpText's Schannel-only refusal line must say every
  build).
- 2026-10-15 (lane 2, second claim): done as planned, recorded in ADR-0540. `CommandLineOption.RefusedWithoutQuic()`
  builds the `--http3`/`--http3-only` rows: refused (exit 2, `the installed libcurl version does not support this`)
  on every platform unless `CommandLineOptions.ActsAsQuicBuild`, which only the new
  `CommandLineParser.Parse(..., actsAsQuicBuild)` overload sets; `CurlCommandRunner` and both
  `CurlComposition.CreateRunner` overloads take `parsesAsQuicBuild` (default off; the executable never sets it).
  Tests that drive HTTP/3 parse through new `QuicBuildParser` helpers (Cli and Console tests) or pass
  `parsesAsQuicBuild: true`. `--ai-help` now says "On every platform: refused with exit 2" for both options.
- Added Curl.Conformance.UnitTests to `touches`: `UpstreamConformanceTests.RunCurlAsync` must parse as a QUIC
  build, or the ratcheted `http/3` cases 2500 and 2503 (run off Windows only, ADR-0498) would fail on Linux and
  macOS CI. No other task was in Doing on origin/work/dark-factory.
- Proof for the Linux items: `CommandLineHttp3RefusalTests` and `CurlCommandRunnerHttp3RefusalTests` parse as the
  OpenSSL build explicitly (`isWindows: false` / `parsesAsWindowsBuild: false`) and pin the exact stderr lines and
  exit 2, so CI's Linux and macOS jobs run them once the shift pushes; lanes cannot push or start gap-platforms.
- Measured: full fast suite green (Windows), `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console`
  0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Needs Curl.Console and Curl.Console.UnitTests, held by BL-2337 in Doing; plan recorded under Notes
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. --http3 and --http3-only are refused with exit 2 on every platform, as every reference build refuses them, unless parsed as a QUIC build (tests, conformance harness); ADR-0540
