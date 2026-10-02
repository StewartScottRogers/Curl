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
completed:
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

- [ ] The three proxy options parse with curl's refusals, with tests in `Curl.Cli.UnitTests`.
- [ ] `ProxyFromCommandLine` maps them, and `FromCommandLine` does not, with tests in `Curl.Console.UnitTests`.
- [ ] `curl -V` lists `TLS-SRP` in curl's position, with a test.
- [ ] `--ai-help` describes the three proxy options correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
