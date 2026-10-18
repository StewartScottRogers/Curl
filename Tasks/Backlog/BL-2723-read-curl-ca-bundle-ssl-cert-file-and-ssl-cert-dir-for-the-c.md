---
id: BL-2723
title: Read CURL_CA_BUNDLE, SSL_CERT_FILE and SSL_CERT_DIR for the CA certificates when no --cacert or --capath is given, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console]
requirement: none
created: 2026-10-18
completed:
---
# BL-2723 — Read CURL_CA_BUNDLE, SSL_CERT_FILE and SSL_CERT_DIR for the CA certificates when no --cacert or --capath is given, as curl does

## Goal

With no `--cacert` or `--capath`, Curl takes its CA certificates from `CURL_CA_BUNDLE`, `SSL_CERT_DIR` and `SSL_CERT_FILE` as curl 8.22.0's tool does, so an HTTPS transfer to a server whose root only those variables name succeeds on Linux and macOS as it does with curl.

## Context

- Measured by gap-platforms run 39108080557 (BL-2721). The server is a loopback TLS server whose certificate a throwaway root issued, and the variable names that root's PEM file, or for `SSL_CERT_DIR` a folder holding it as `<subject hash>.0`.
  - curl exits 0 in every case.
  - Curl exits 60 ("unable to get local issuer certificate") for `CURL_CA_BUNDLE` on Linux, and for `CURL_CA_BUNDLE`, `SSL_CERT_FILE` and `SSL_CERT_DIR` on macOS.
- Curl reads none of the three variables. No `.cs` file names them. `SSL_CERT_FILE` and `SSL_CERT_DIR` match on Linux only because the .NET runtime's OpenSSL reads them itself. On macOS, .NET uses Apple's trust store, so none of them takes effect.
- Upstream: curl-8_22_0 `src/tool_operate.c` (look for `CURL_CA_BUNDLE`, `SSL_CERT_DIR` and `SSL_CERT_FILE`, and the order it reads them in, with `--cacert`/`--capath` winning). Also `docs/cmdline-opts/_ENVIRONMENT.md` and `--ca-native`. Read whether the Schannel build on Windows reads them too, and match the platform's curl (CLAUDE.md "Decisions"). The gap office's Windows measurement already matches; keep it matching.
- The environment is already read through `CurlComposition`'s `readEnvironmentVariable` (`Curl.Console/CurlComposition.cs`, BL-2716 for `SSLKEYLOGFILE`). The CA file and folder then go wherever `--cacert` and `--capath` already go.
- A platform-neutral unit test needs no real TLS: a fake environment reader and the options the CLI hands the engine.

## Acceptance criteria

- [ ] With no `--cacert`/`--capath`, `CURL_CA_BUNDLE` sets the CA file and `SSL_CERT_DIR`/`SSL_CERT_FILE` the CA path/file in curl 8.22.0's order, and `--cacert`/`--capath` still win, each pinned by a unit test in `Curl.Cli.UnitTests` (or the project that owns the mapping).
- [ ] An ADR records what each platform's curl reads, from upstream's source and a measurement of real curl, decided by Claude under Stewart's delegation.
- [ ] `--ai-help` names the three variables wherever it lists environment variables.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members in the changed libraries.

## Notes

## Log

- 2026-10-18: Created.
