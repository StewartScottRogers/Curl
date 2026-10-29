---
id: BL-4045
title: Set TlsClientOptions.TracesHandshakeMessages from -v and --trace so the OpenSSL build prints the handshake message trace
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4010]
touches: [Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/OptionGroupConnectionSettings.cs, Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4045 — Set TlsClientOptions.TracesHandshakeMessages from -v and --trace so the OpenSSL build prints the handshake message trace

## Goal

Off Windows, `curl -v https://...` (and `--trace`, `--trace-ascii`) runs the origin's and an HTTPS proxy's handshake on the hand-built client, so it prints OpenSSL's handshake message trace (`* TLSv1.3 (OUT), TLS handshake, Client hello (1):`), as curl 8.18.0 OpenSSL does (CGR-226).

## Context

BL-4010 (ADR-0984) added `TlsClientOptions.TracesHandshakeMessages` and the `TlsClientRouting` row that routes such a connection to `HandBuiltTlsProvider` in the OpenSSL build; nothing sets the option yet. `TlsClientOptionsMapping.Of` and `ProxyOf` build the options from `EasyTlsOptions`, called from `EngineComposition.Transports.cs` (line ~134) and `OptionGroupConnectionSettings.cs` (line ~49); find where the engine knows `-v`/`--trace` (`EngineToolOptions` or the trace components, `CURLOPT_VERBOSE`/debug callback for libcurl) and pass it in. Set it only when the trace would be printed, so a transfer without `-v` keeps `SslStream`.

## Acceptance criteria

- [ ] With `-v` the origin's `TlsClientOptions` has `TracesHandshakeMessages` set, and without it does not, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-28: Created.
