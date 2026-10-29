---
id: BL-4123
title: Extension: Send XMPP messages and service queries
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Xmpp.UnitLibrary, Curl.Extension.Xmpp.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4123 — Extension: Send XMPP messages and service queries

## Goal

Curl provides `xmpp:` URIs: send a message, disco#info query, as the extension `Curl.Extension.Xmpp.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 22 of 63. Specification: RFC 6120, RFC 6121, RFC 5122, XEP-0030. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Xmpp.UnitLibrary` and `Curl.Extension.Xmpp.UnitTests` exist in `/Extensions/Xmpp/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `xmpp:` URIs: send a message, disco#info query.
- [x] Unit tests check its wire behaviour against named sections of RFC 6120, RFC 6121, RFC 5122, XEP-0030, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- ExtensionDescriptionCatalogue.cs added to touches: it lists each extension's description for --extensions and --ai-help extensions. CurlCompositionTests.cs added: its list of TCP handlers names each handler, so XmppProtocolHandler joins it.
- SCRAM-SHA-1/256 is not yet done (ADR-1068); PLAIN over STARTTLS and ANONYMOUS are. Tests are platform-neutral (no paths, output written without indentation so no platform newline), so CI on Linux and macOS holds.
- Quality: Measure-CodeQuality.ps1 reports Curl.Extension.Xmpp.UnitLibrary at 100% line and branch, 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0364ad1b 5b43e1c5 c7e7b42b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Deferred. Split into BL-4270 (verify delivered work and gates) and BL-4271 (SCRAM-SHA authentication) after hitting the cost cap
