---
id: BL-4149
title: Extension: Resolve names over Oblivious DoH
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4096, BL-4100]
touches: [Curl.Extension.ObliviousDoh.UnitLibrary, Curl.Extension.ObliviousDoh.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionResolverHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4149 — Extension: Resolve names over Oblivious DoH

## Goal

Curl provides an option naming the ODoH proxy and target, as the extension `Curl.Extension.ObliviousDoh.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 49 of 63. Specification: RFC 9230. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.ObliviousDoh.UnitLibrary` and `Curl.Extension.ObliviousDoh.UnitTests` exist in `/Extensions/ObliviousDoh/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option naming the ODoH proxy and target.
- [x] Unit tests check its wire behaviour against named sections of RFC 9230, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 13): Done as a resolver hook (ADR-1012), recorded in ADR-1117: `--odoh-proxy <URL>` and `--odoh-target <URL>`, given together. Oblivious DoH needs no scheme, so like EncryptedDns it adds no line to ExtensionCatalogue.cs (whose entries are scheme handlers) and registers one line each in the option (two options), description and resolver hook catalogues, shared list files added to `touches`; the first criterion is read that way, as BL-4105 and BL-4107 did. VerifyExtensionIsolation passes in the build.
- Defaults taken: configs fetched from `/.well-known/odohconfigs` on the target (RFC 9230 leaves discovery out of scope), first usable entry kept per resolver; HPKE from Curl.Cryptography (P-256 or X25519, HKDF-SHA256, AES-GCM or ChaCha20-Poly1305); query padding to 128-octet blocks (RFC 8467); one HTTP/1.1 request per connection with `Connection: close`; DNS codec from Curl.Networking's public DnsQueryEncoder and DnsAnswerDecoder. Any failure gives no addresses (exit 6).
- Tests: the target's side is written in the tests from sections 6.3 and 6.4 (TestTarget) and opens the client's queries and seals answers the client opens; fake connectors, no network. 90 tests.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Extension.ObliviousDoh.UnitLibrary,Curl.Extension.Composition.UnitLibrary: both 100% line and branch; SelectFrom was split for complexity 14, re-measured: worst CRAP 10, 0 failing. Checked by hand: `curl --extensions` and `curl --ai-help extensions` list oblivious-doh; with CURL_EXTENSIONS=0 `--odoh-proxy` exits 2 "option --odoh-proxy: is unknown". dotnet build clean, fast tests green (Windows; tests are platform-neutral).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 84228abe 49cf50ab bd157199 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Curl resolves host names over Oblivious DoH (RFC 9230) with --odoh-proxy and --odoh-target, as the extension oblivious-doh
