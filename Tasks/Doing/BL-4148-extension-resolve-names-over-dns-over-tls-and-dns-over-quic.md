---
id: BL-4148
title: Extension: Resolve names over DNS-over-TLS and DNS-over-QUIC
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4096, BL-4200, BL-4207, BL-4208]
touches: [Curl.Extension.EncryptedDns.UnitLibrary, Curl.Extension.EncryptedDns.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionResolverHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4148 — Extension: Resolve names over DNS-over-TLS and DNS-over-QUIC

## Goal

Curl provides options naming a DoT or DoQ resolver, as the extension `Curl.Extension.EncryptedDns.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 48 of 63. Specification: RFC 7858 and RFC 9250. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.EncryptedDns.UnitLibrary` and `Curl.Extension.EncryptedDns.UnitTests` exist in `/Extensions/EncryptedDns/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides options naming a DoT or DoQ resolver.
- [ ] Unit tests check its wire behaviour against named sections of RFC 7858 and RFC 9250, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): split for size before any code. BL-4207 makes the projects, the DNS message codec and the DoT resolver (RFC 7858); BL-4208 the DoQ resolver over `Curl.Quic.UnitLibrary` (RFC 9250, ALPN `doq`). What stays here is the options (default choice: `--dns-over-tls` and `--dns-over-quic`, each naming a resolver `host[:port]`, to be recorded in the ADR), the `IResolverHook`, its description and option catalogue lines, the resolver hook catalogue line, the ADR and the gates. No host passes a resolver hook to the engine yet: BL-4200 adds `ExtensionResolverHookCatalogue.cs` and the Console wiring, so it is a dependency. The hook gets only curl's `IDnsResolver`, so the activator builds the production connectors from `Curl.Networking.UnitLibrary` (`TcpConnector`, a TLS provider, `UdpDatagramConnector`), a shared layer `VerifyExtensionIsolation` allows. The three catalogue files added to touches are shared list files (one line each).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 527d758d dd8314a7 8c65f771 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4207 (projects, DNS codec, DoT resolver), BL-4208 (DoQ resolver) and BL-4200 (Console passes the resolver hook to the engine)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 527d758d dd8314a7 8c65f771 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
