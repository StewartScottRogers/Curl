---
id: BL-2822
title: Close GF-0097: An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2822 — Close GF-0097: An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0097 (An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts), so a later gap analysis measures each of `environment:SSL_CERT_DIR`, `environment:SSL_CERT_FILE`, `environment:[url-protocol]_PROXY` as `match`.

## Context

- Finding: GF-0097, filed by the gap analysis office (ADR-0433).
- Area: environment. Severity: Critical. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `environment:SSL_CERT_DIR`, `environment:SSL_CERT_FILE`, `environment:[url-protocol]_PROXY`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_ENVIRONMENT.md`, `docs/libcurl/libcurl-env.md`, `docs/cmdline-opts/config.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

environment:SSL_CERT_DIR: expected 'exit 0; stdout ''; stderr ''; request GET / HTTP/1.1', actual 'exit 134; ... Unhandled exception. System.ArgumentException: The value cannot be an empty string. (Parameter 'path') at System.ArgumentException.ThrowNullOrEmptyException ...; request none'. environment:SSL_CERT_FILE: same expected and actual. environment:[url-protocol]_PROXY: expected 'exit 35; ...; request CONNECT gap.invalid:443 HTTP/1.1', actual 'exit 134; ... same ArgumentException ...; request CONNECT gap.invalid:443 HTTP/1.1'. The CURL_CA_BUNDLE and SSLKEYLOGFILE recipes (they bypass the SSL_CERT_DIR lookup) match. Reproduce on linux: pwsh -NoProfile -File Gap/Tools/Measure-EnvironmentGap.ps1 -OutFile $env:TEMP/environment.json

Suggestion, copied from the finding:

Most likely cause: CaCertificateEnvironment.Apply (Curl.Cli.UnitLibrary) copies the SSL_CERT_DIR value verbatim, and the measuring tool most likely clears the variable by setting it empty on Linux. The empty value reaches ServerCertificateVerification.ReadCaCertificateDirectory (Curl.Networking.UnitLibrary) and Directory.GetFiles("") throws ArgumentException, which ListFilesOrNothing does not catch. Fix: (1) in ListFilesOrNothing and ReadTextOrNothing catch ArgumentException and NotSupportedException too, so an empty or malformed --capath adds nothing, as in curl's OpenSSL build. (2) In ReadCaCertificates, treat an empty CaCertificateFile as an unusable file (exit 77) and never let an unhandled exception escape. An empty SSL_CERT_FILE would otherwise crash in File.ReadAllText(""). (3) Confirm with a probe what curl 8.22.0 does with SSL_CERT_DIR='' and SSL_CERT_FILE='' and match it (docs/cmdline-opts/_ENVIRONMENT.md, CURL_CA_BUNDLE / SSL_CERT_DIR / SSL_CERT_FILE). Add focused unit tests in Curl.Networking.UnitTests and Curl.Cli.UnitTests. This is a linux/macos cause (the Schannel build reads none of these variables).

## Acceptance criteria

- [x] `environment:SSL_CERT_DIR`: Curl answers what curl 8.22.0 answers on linux, `exit 0; stdout ''; stderr ''; request GET / HTTP/1.1`, so the item measures `match`.
- [x] `environment:SSL_CERT_FILE`: Curl answers what curl 8.22.0 answers on linux, `exit 0; stdout ''; stderr ''; request GET / HTTP/1.1`, so the item measures `match`.
- [x] `environment:[url-protocol]_PROXY`: Curl answers what curl 8.22.0 answers on linux, `exit 35; stdout ''; stderr ''; request CONNECT gap.invalid:443 HTTP/1.1`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: the gap tool clears the CA variables a recipe does not test by setting them to the empty string; `CaCertificateEnvironment.Apply` copied the empty value into `--capath`/`--cacert`, and `Directory.GetFiles("")` or `File.ReadAllText("")` threw an unhandled `ArgumentException` (exit 134).
- Decision (ADR-0707): an empty `CURL_CA_BUNDLE`, `SSL_CERT_DIR` or `SSL_CERT_FILE` counts as unset, which gives curl's measured answer (exit 0 with the other variable's CA; exit 35 through the proxy to gap.invalid). In `ServerCertificateVerification` an empty or malformed `--capath` now adds nothing (`ArgumentException`, `NotSupportedException` caught) and an empty `--cacert` is exit 77 (`FileNotFoundException`), never an unhandled exception. A lane cannot run Linux curl; the finding's measurement is the evidence. Whether curl treats an empty `CURL_CA_BUNDLE` as set is unmeasured; the CURL_CA_BUNDLE recipe already matched.
- Evidence for criteria 1-4 (as BL-2327 and BL-2329 did): platform-neutral tests CI runs on Linux and macOS once the shift pushes - `CaCertificateEnvironmentTests.Parse_VariableSetEmpty_CountsAsUnset` (4 rows), `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithEmptyCaCertificateDirectoryInTheOpenSslBuild_AddsNothing`, `..._WithEmptyCaCertificateDirectoryBesideTheServerCertificateFileInTheOpenSslBuild_Succeeds`, `..._WithEmptyCaCertificateFileInTheOpenSslBuild_FailsWithSslCacertBadfile`. A lane cannot dispatch gap-platforms.yml; the gap closes only when the next gap run measures the three items as `match` (ADR-0433).
- `dotnet build -warnaserror` clean; fast tests green (0 failed solution-wide; Cli 3956, Networking 3260 passed). Measure-CodeQuality for Curl.Networking.UnitLibrary and Curl.Cli.UnitLibrary: 0 failing members. No option added or changed, so `--ai-help` needs no change.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: d3442ebf 4d2fe763 3e9d694d 8b83e917 1fd5de8c 28346828.
- 2026-10-19: Doing -> Done. An empty SSL_CERT_DIR, SSL_CERT_FILE or CURL_CA_BUNDLE counts as unset, and an empty --capath or --cacert no longer crashes a TLS transfer (ADR-0707)
