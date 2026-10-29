---
id: BL-4202
title: Extension: TLS over a --npipe named pipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4142]
touches: [Curl.Extension.Abstractions.UnitLibrary/Connection, Curl.Extension.Abstractions.UnitTests/Connection, Curl.Extension.NamedPipe.UnitLibrary, Curl.Extension.NamedPipe.UnitTests, Curl.Console/CurlComposition.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4202 — Extension: TLS over a --npipe named pipe

## Goal

`curl --npipe \\.\pipe\name https://host/` runs TLS over the named pipe with curl's TLS options (`-k`, `--cacert`, ALPN), as `--unix-socket` does for `https://`, instead of failing with exit 4.

## Context

BL-4142 (ADR-1017) added `--npipe` as the extension `Curl.Extension.NamedPipe.UnitLibrary`. Its `NamedPipeConnector` replaces curl's whole connector through `IConnectionHook` (ADR-1010), and curl's TLS handshake lives inside curl's connector, which an extension cannot reach, so a TLS target is refused with exit 4. This needs a seam in `Curl.Extension.Abstractions` (for example a hook that supplies only the byte stream a connection runs over, with curl's connector layering TLS, proxies and the HTTP version on top), wired by `Curl.Console`'s `CurlComposition` and the engine's connector factory.

## Acceptance criteria

- [ ] An `https://` transfer under `--npipe` completes its TLS handshake over the pipe with the group's TLS options; unit tests over a fake pipe with recorded bytes.
- [ ] `NamedPipeConnector` no longer refuses a TLS target, and ADR-1017's "Left out" item is updated by a new or amended ADR marked "Decided by Claude under Stewart's delegation".
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
