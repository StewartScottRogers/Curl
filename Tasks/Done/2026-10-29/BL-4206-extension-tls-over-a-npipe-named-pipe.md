---
id: BL-4206
title: Extension: TLS over a --npipe named pipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4142]
touches: [Curl.Extension.Abstractions.UnitLibrary/Connection, Curl.Extension.Abstractions.UnitTests/Connection, Curl.Extension.NamedPipe.UnitLibrary, Curl.Extension.NamedPipe.UnitTests, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionConnectionHookTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4206 — Extension: TLS over a --npipe named pipe

## Goal

`curl --npipe \\.\pipe\name https://host/` runs TLS over the named pipe with curl's TLS options (`-k`, `--cacert`, ALPN), as `--unix-socket` does for `https://`, instead of failing with exit 4.

## Context

BL-4142 (ADR-1021) added `--npipe` as the extension `Curl.Extension.NamedPipe.UnitLibrary`. Its `NamedPipeConnector` replaces curl's whole connector through `IConnectionHook` (ADR-1010), and curl's TLS handshake lives inside curl's connector, which an extension cannot reach, so a TLS target is refused with exit 4. This needs a seam in `Curl.Extension.Abstractions` (for example a hook that supplies only the byte stream a connection runs over, with curl's connector layering TLS, proxies and the HTTP version on top), wired by `Curl.Console`'s `CurlComposition` and the engine's connector factory.

## Acceptance criteria

- [x] An `https://` transfer under `--npipe` completes its TLS handshake over the pipe with the group's TLS options; unit tests over a fake pipe with recorded bytes.
- [x] `NamedPipeConnector` no longer refuses a TLS target, and ADR-1021's "Left out" item is updated by a new or amended ADR marked "Decided by Claude under Stewart's delegation".
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1055): `IConnectionHook` gains a default `ConnectorOver(curlConnector, curlTlsProvider)`, `ConnectionHooks` the matching overload, and `CurlComposition.ExtensionConnectorOf` hands the hook the option groups TLS provider (built from `-k`, `--cacert` and the rest). `NamedPipeConnector` opens the pipe and runs `AuthenticateAsClientAsync(pipe, TlsServerName ?? Host, target.Events)` over it; with no provider it still refuses TLS with exit 4. The engine needed no change.
- Choice: the seam hands the TLS provider rather than adding a stream-only hook to the engines connector factory, because the engine is outside this tasks touches and BL-4249 holds Curl.Networking and Curl.Protocol.Abstractions; the provider carries every TLS option but ALPN.
- ALPN is left to BL-4263 (filed): the public `ITlsProvider` overloads offer no ALPN, so HTTPS over the pipe runs HTTP/1.1 until it lands. ADR-1021 is left as it is; ADR-1055 says it replaces that ADRs first Left out item.
- Added Curl.Console.UnitTests/ExtensionConnectionHookTests.cs to touches: `ExtensionConnectorOf` took a new parameter, so its tests changed; no task in Doing on origin/work/dark-factory names it.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary,Curl.Extension.NamedPipe.UnitLibrary,Curl.Console: all three 100% line and branch, 0 failing members. dotnet build clean; fast tests exit 0. The tests use only in-memory fakes, so they do not depend on the platform.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ab2f5cba 7090b3af 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. https:// under --npipe runs curl's TLS handshake over the pipe with the group's TLS provider (-k, --cacert); ALPN is BL-4263
