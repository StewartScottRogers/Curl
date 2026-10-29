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
completed: 2026-10-29
---
# BL-4148 — Extension: Resolve names over DNS-over-TLS and DNS-over-QUIC

## Goal

Curl provides options naming a DoT or DoQ resolver, as the extension `Curl.Extension.EncryptedDns.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 48 of 63. Specification: RFC 7858 and RFC 9250. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.EncryptedDns.UnitLibrary` and `Curl.Extension.EncryptedDns.UnitTests` exist in `/Extensions/EncryptedDns/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides options naming a DoT or DoQ resolver.
- [x] Unit tests check its wire behaviour against named sections of RFC 7858 and RFC 9250, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): split for size before any code. BL-4207 makes the projects, the DNS message codec and the DoT resolver (RFC 7858); BL-4208 the DoQ resolver over `Curl.Quic.UnitLibrary` (RFC 9250, ALPN `doq`). What stays here is the options (default choice: `--dns-over-tls` and `--dns-over-quic`, each naming a resolver `host[:port]`, to be recorded in the ADR), the `IResolverHook`, its description and option catalogue lines, the resolver hook catalogue line, the ADR and the gates. No host passes a resolver hook to the engine yet: BL-4200 adds `ExtensionResolverHookCatalogue.cs` and the Console wiring, so it is a dependency. The hook gets only curl's `IDnsResolver`, so the activator builds the production connectors from `Curl.Networking.UnitLibrary` (`TcpConnector`, a TLS provider, `UdpDatagramConnector`), a shared layer `VerifyExtensionIsolation` allows. The three catalogue files added to touches are shared list files (one line each).
- 2026-10-29 (lane 11): Plan: `EncryptedDnsExtension` gives `--dns-over-tls <host[:port]>` and `--dns-over-quic <host[:port]>` (port 853 by default, parsed by `EncryptedDnsServerAddress`), the `encrypted-dns` description and one resolver hook activator per option, so `ResolverHooks.ActivatedBy` refuses the two together or beside `--doh-url`. The hooks build the resolvers over `Curl.Networking.UnitLibrary` (`TcpConnector` + `SslStreamTlsProvider`; `UdpDatagramConnector`); DoQ's certificate goes through the new `TlsProviderCertificateVerifier` to `ITlsProvider.VerifyServerCertificateChain`, since Networking's hand-built verifier is internal. Decisions in ADR-1070 (curl's TLS options and a separate authentication name left out, with why).
- Registration: as BL-4142, `ExtensionCatalogue.cs` holds protocol handler factories only and this extension adds no scheme, so its registration lines are in `ExtensionResolverHookCatalogue.cs` (two activators), `ExtensionOptionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs`, all already in `touches`. The project was already in `/Extensions/EncryptedDns/` in `Curl.slnx` (BL-4207); the build's `VerifyExtensionIsolation` passes with the new `Curl.Networking.UnitLibrary` reference.
- Measured on the built curl.exe: a bad value (`dot.example:0`) gives exit 2 with `--dns-over-tls needs a resolver as host[:port], not 'dot.example:0'`; `CURL_EXTENSIONS=0` gives `option --dns-over-tls: is unknown`, exit 2; both options give exit 2 naming both; a resolver that refuses gives exit 6. Live: `--dns-over-tls one.one.one.one http://example.com/` and `--dns-over-quic dns.adguard-dns.com http://example.com/` both fetched 200. `curl --extensions` and `curl --ai-help extensions` list `encrypted-dns` with both options.
- Wire behaviour: the resolvers' tests from BL-4207 and BL-4208 cite RFC 7858 sections 3.1 to 3.3 and RFC 9250 sections 4.1 to 4.5 over fake connections; this run adds the default-port tests (RFC 7858 section 3.1, RFC 9250 section 4.1) and the hooks' tests, none touching the network.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.EncryptedDns.UnitLibrary,Curl.Extension.Composition.UnitLibrary` found `EncryptedDnsServerAddress.Split` at complexity 12; split into `SplitBracketed` and `SplitUnbracketed`, and the EncryptedDns re-run gave 100/100, 0 failing members; Composition 100/100, 0 failing.
- Platform: the tests use no path, OS error text or certificate, so they are platform-neutral; CI on Linux and macOS is covered by that.
- Fast tests: 68 test projects passed, 0 failed; the EncryptedDns project has 85+ tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 527d758d dd8314a7 8c65f771 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4207 (projects, DNS codec, DoT resolver), BL-4208 (DoQ resolver) and BL-4200 (Console passes the resolver hook to the engine)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 527d758d dd8314a7 8c65f771 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --dns-over-tls and --dns-over-quic look host names up on a DoT or DoQ resolver, as the encrypted-dns extension
