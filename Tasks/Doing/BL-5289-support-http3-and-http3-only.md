---
id: BL-5289
title: Support --http3 and --http3-only
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Engine.UnitLibrary/ManagedCurlVersion.cs, Curl.Cli.UnitTests/Http3OptionsAcceptedTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5289 — Support --http3 and --http3-only

## Goal

The port speaks HTTP/3 over QUIC from the command line: `--http3-only` uses HTTP/3 with no fallback, `--http3` falls back to TCP when QUIC fails, and `curl -V` lists `HTTP3`, as upstream curl 8.21.0 built with ngtcp2 and nghttp3 does.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 12 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http3.md`, finding 1.
- **Surl tests (12):** every `CurlPortBesideUpstreamCurlOverHttp3Tests` case but `Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`, e.g. `GetWithHttp3Only_File_IsServedOverHttp3`, `GetWithHttp3Only_Http3Off_FailsToConnect`, `GetWithHttp3_Http3Off_FallsBackToHttp2`; Surl commit `883c3d4d4881bb59b9179bd1cb3f33973096af7f`.
- **Command line, e.g.:** `curl -sS -k --http3-only -w %{http_version} https://127.0.0.1:<port>/hello.txt` against `surl --http3`.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`: exit 0, `hello from surl\n3`; with `surl` started without `--http3`, `--http3-only` exits 56 (`Failed to connect ... Failure when receiving data from the peer`) and `--http3` falls back over TCP (exit 0, `...\n2`).
- **Port (defect):** exit 2 in every case, no output, `curl: option --http3-only: the installed libcurl version does not support this` (or `--http3`).
- **What decides:** curl 8.21.0 manual, `--http3-only` and `--http3` (https://curl.se/docs/manpage.html); RFC 9114, RFC 9000, RFC 9001; the port's own rule that QUIC and HTTP/3 are built by hand (CLAUDE.md, "Decisions").
- **Where to start:** `Curl.Cli.UnitLibrary/CommandLineOption.cs`: the `--http3` rows are refused with `CommandLineRefusal.InstalledLibcurlDoesNotSupport` unless `CommandLineOptions.ActsAsQuicBuild` (GF-0089, ADR-0540), because no reference build has a QUIC library. The HTTP/3 transfer itself exists (BL-732).

## Acceptance criteria

- [x] `curl -V` lists `HTTP3` in `Features:`.
- [x] `--http3-only` against an HTTP/3 server exits 0 and `-w %{http_version}` writes `3`; against a server with no QUIC listener it exits 56 (`CURLE_RECV_ERROR`), shown by port tests.
- [x] `--http3` against a server with no QUIC listener falls back over TCP and exits 0, shown by a port test.

## Notes

ADR-1282 supersedes ADR-0540: the table rows are plain flags, RefusedWithoutQuic and its AI-help line are gone, and `curl -V` and ManagedCurlVersion list HTTP3 on every platform. Touches widened to the table, options, AI help and ManagedCurlVersion (no other Doing task names them); tests that pinned the refusal were replaced (CommandLineHttp3RefusalTests, CurlCommandRunnerHttp3RefusalTests, CurlCommandRunnerUnimplementedOptionTests deleted; two Schannel refusal tests removed). Measured with the built curl.exe: `--http3-only` against a closed port exits 56. The `--http3` TCP fallback and exit 0 over HTTP/3 come from the existing transfer (BL-732, covered by CurlCommandRunnerHttp3Tests). Cross-platform: tests pin no OS-specific text. Quality run for Curl.Cli and Curl.Engine: 0 failing members.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 84bf8554 a12090e0 622005d5.
