---
id: BL-1135
title: Parse --proxy-tlsuser, --proxy-tlspassword and --proxy-tlsauthtype and list TLS-SRP in curl -V
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-712]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1135 — Parse --proxy-tlsuser, --proxy-tlspassword and --proxy-tlsauthtype and list TLS-SRP in curl -V

## Goal

`--proxy-tlsuser u --proxy-tlspassword p` (with `--proxy-tlsauthtype SRP`) authenticates the HTTPS proxy's TLS handshake with SRP on every platform, and `curl -V` lists `TLS-SRP` among its features.

## Context

- Split from BL-712, which wired `--tlsuser`, `--tlspassword` and `--tlsauthtype` to the hand-built client's SRP (ADR-0328, ADR-0229) but could not touch `Curl.Cli.UnitLibrary` while BL-1099 held it.
- The three proxy options are only in `CurlOptionAliasTable` and `CurlHelpTable` today; parse them as `--tlsuser`, `--tlspassword` and `--tlsauthtype` are parsed in `CommandLineOptionTable` (blank refusal, `ALLOW_BLANK` password, `SRP` only, case-sensitive), add `ProxyTlsUser`, `ProxyTlsPassword`, `ProxyTlsAuthType` to `CommandLineOptions`, and map them in `TlsClientOptionsMapping.ProxyFromCommandLine` onto `TlsUser`, `TlsPassword`, `TlsAuthType`. `TlsClientRouting` and `HandBuiltTlsProvider` then route and run them with no Networking change.
- `curl -V`: `CurlVersionText.FeaturesLine` (ADR-0021: list only what Curl implements). WSL's curl 8.18.0 OpenSSL build lists `TLS-SRP` between `threadsafe` and `UnixSockets`.
- Measure the proxy forms with an OpenSSL build of curl before pinning text.

## Acceptance criteria

- [x] The three proxy options parse with curl's refusals, with tests in `Curl.Cli.UnitTests`.
- [x] `ProxyFromCommandLine` maps them, and `FromCommandLine` does not, with tests in `Curl.Console.UnitTests`.
- [x] `curl -V` lists `TLS-SRP` in curl's position, with a test.
- [x] `--ai-help` describes the three proxy options correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured with WSL curl 8.18.0 (OpenSSL) on 2026-10-02: `--proxy-tlsuser ""` is accepted and `--proxy-tlspassword ""` is refused as blank (the reverse of `--tlsuser` and `--tlspassword`); `--proxy-tlsauthtype` refuses blank and `srp` as `--tlsauthtype` does; none of the three takes `--no-`. Pinned in `CommandLineProxyTlsSrpOptionTests`.
- `curl -V` lists `TLS-SRP` right after `SSL`: curl sorts case-insensitively, and Curl lists neither `threadsafe` nor `UnixSockets`.
- With these three rows every curl option that takes no `--no-` prefix has a row, so the "cannot be reversed" branch of `CommandLineParser.RefuseUnlistedNegation` became unreachable and broke 100% branch coverage. It now delegates to `RefuseUnlistedName`; `CommandLineUnimplementedOptionTests` still fails should such a row be removed.
- The three options wait for BL-1170 in `LibcurlSourceCodeOptionCoverageTests` (their `--libcurl` lines).
- `--ai-help` no longer says "Not supported by this build yet" for them, with a test.
- Coverage: `Curl.Cli.UnitLibrary` and `Curl.Console` 100% line and branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. proxy TLS-SRP options parse with curl refusals and reach the proxy handshake; curl -V lists TLS-SRP
