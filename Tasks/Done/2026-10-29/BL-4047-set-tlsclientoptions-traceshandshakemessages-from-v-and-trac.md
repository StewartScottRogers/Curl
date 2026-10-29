---
id: BL-4047
title: Set TlsClientOptions.TracesHandshakeMessages from -v and --trace so the OpenSSL build prints the handshake message trace
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4010, BL-3766]
touches: [Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Engine.UnitLibrary/EngineToolOptions.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/OptionGroupConnectionSettings.cs, Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4047 — Set TlsClientOptions.TracesHandshakeMessages from -v and --trace so the OpenSSL build prints the handshake message trace

## Goal

Off Windows, `curl -v https://...` (and `--trace`, `--trace-ascii`) runs the origin's and an HTTPS proxy's handshake on the hand-built client, so it prints OpenSSL's handshake message trace (`* TLSv1.3 (OUT), TLS handshake, Client hello (1):`), as curl 8.18.0 OpenSSL does (CGR-226).

## Context

BL-4010 (ADR-0985) added `TlsClientOptions.TracesHandshakeMessages` and the `TlsClientRouting` row that routes such a connection to `HandBuiltTlsProvider` in the OpenSSL build; nothing sets the option yet. `TlsClientOptionsMapping.Of` and `ProxyOf` build the options from `EasyTlsOptions`, called from `EngineComposition.Transports.cs` (line ~134) and `OptionGroupConnectionSettings.cs` (line ~49); find where the engine knows `-v`/`--trace` (`EngineToolOptions` or the trace components, `CURLOPT_VERBOSE`/debug callback for libcurl) and pass it in. Set it only when the trace would be printed, so a transfer without `-v` keeps `SslStream`.

## Acceptance criteria

- [x] With `-v` the origin's `TlsClientOptions` has `TracesHandshakeMessages` set, and without it does not, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-28: -v and --trace are known only to Curl.Console (EasyOptionsFromCommandLine.ToolOptionsOf, a new EngineToolOptions member) and to ManagedCurlEngine.CreateTransports (options.Behaviour.Verbose for libcurl). BL-3766 holds the whole of Curl.Console in Doing, so added those files to touches and BL-3766 to depends-on; requeued before any edit.
- 2026-10-28: Decided (sensible default): a new `EngineToolOptions.TracesTransfer` (default false) carries `-v`/`--trace`/`--trace-ascii` (`CommandLineOptions.Trace != None`) from Curl.Console and `CURLOPT_VERBOSE` (`EasyBehaviourOptions.Verbose`) from ManagedCurlEngine; `TlsClientOptionsMapping.Of`/`ProxyOf` take it as an optional `tracesHandshakeMessages`, so the target and HTTPS proxy both route to the hand-built client in the OpenSSL build only when the trace is printed, and the connection-cache key differs between traced and untraced groups, as their TLS stacks do. DoH keeps SslStream (its handshake is not the target's). The conformance harness's dialing path in Curl.Console/CurlComposition.cs (outside touches) still maps without it; it is not the production path. Pinned by TlsClientOptionsMappingTests.Of_VerboseOrTraceOnCommandLine_TracesHandshakeMessagesOfTargetAndProxy. Measure-CodeQuality: 0 failing members for Curl.Engine.UnitLibrary and Curl.Console. Change is platform-neutral; fast tests green on Windows.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 5731c438 3d836d5e.
- 2026-10-28: Doing -> Backlog. Needs Curl.Console/EasyOptionsFromCommandLine.cs, held by BL-3766 (whole Curl.Console); depends-on BL-3766
- 2026-10-28: Backlog -> Doing. Criteria at claim: 5731c438 3d836d5e.
- 2026-10-28: Doing -> Done. -v and --trace set TracesHandshakeMessages on the target's and HTTPS proxy's TLS options
