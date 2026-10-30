---
id: BL-4326
title: Scaffold the WireGuard extension and parse wg-quick configuration files
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/Curl.Extension.WireGuard.UnitLibrary.csproj, Curl.Extension.WireGuard.UnitLibrary/Configuration, Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md, Curl.Extension.WireGuard.UnitTests/Curl.Extension.WireGuard.UnitTests.csproj, Curl.Extension.WireGuard.UnitTests/Configuration, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4326 — Scaffold the WireGuard extension and parse wg-quick configuration files

## Goal

`Curl.Extension.WireGuard.UnitLibrary` and `Curl.Extension.WireGuard.UnitTests` exist in `/Extensions/WireGuard/` in `Curl.slnx` and parse a wg-quick style configuration file into a typed configuration.

## Context

Split from BL-4147. Extension rules: ADR-0999 and CLAUDE.md "Extensions"; new project with the `new-project` skill; projects in `Curl.slnx` as one single-line `<Folder>` under the marker comment (ADR-1003). The extension may reference `Curl.Cryptography.UnitLibrary` (as `Curl.Extension.ObliviousHttp.UnitLibrary` does), `Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Extension.Abstractions.UnitLibrary`, never a `Curl.Protocol.*` library or another extension. Registration in `ExtensionCatalogue.cs` and the `--wireguard` option are left to BL-4147, which wires the whole flow.

Configuration format (wg(8) "CONFIGURATION FILE FORMAT" and wg-quick(8)): `[Interface]` with `PrivateKey` (base64, 32 bytes), `Address` (one or more CIDR, comma separated), `DNS` (optional), `ListenPort`, `MTU`; one or more `[Peer]` with `PublicKey`, `PresharedKey` (optional), `Endpoint` (host:port, `[v6]:port`), `AllowedIPs`, `PersistentKeepalive`. `#` comments, case-insensitive keys, whitespace around `=`.

## Acceptance criteria

- [ ] Both projects exist in `/Extensions/WireGuard/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] `Configuration/` parses every key listed above into a typed configuration, and rejects with a message naming the line a missing `PrivateKey`, a key that is not 32 bytes of base64, a malformed CIDR or endpoint, an unknown section, and a file with no `[Peer]`; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c10598a7 ec1e0714 bef89300 3d836d5e.
