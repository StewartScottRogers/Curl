---
id: BL-4151
title: Extension: Check DNSSEC and DANE TLSA records
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4096, BL-4097, BL-4333, BL-4334, BL-4335, BL-4331, BL-4332]
touches: [Curl.Extension.Dane.UnitLibrary, Curl.Extension.Dane.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCertificateCheckHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4151 — Extension: Check DNSSEC and DANE TLSA records

## Goal

Curl provides an option that validates DNSSEC and checks the server certificate against TLSA, as the extension `Curl.Extension.Dane.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 51 of 63. Specification: RFC 4033-4035 and RFC 6698/7671. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Dane.UnitLibrary` and `Curl.Extension.Dane.UnitTests` exist in `/Extensions/Dane/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that validates DNSSEC and checks the server certificate against TLSA.
- [x] Unit tests check its wire behaviour against named sections of RFC 4033-4035 and RFC 6698/7671, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 11, first pass before any code): too big for one run, split per the note above. BL-4333 scaffolds the library and the DNSSEC-aware DNS codec (`Wire/`); BL-4334 matches a chain against TLSA records (`Tlsa/`); BL-4335 verifies RRSIG/DNSKEY/DS and holds the root anchors (`Dnssec/`); BL-4332 looks up and validates the TLSA RRset over UDP/TCP from the root (`Lookup/`). A missing hook was also found: `ICertificateCheckHook.RefusalOfAsync` gives a check the host but not the port, which the TLSA name `_<port>._tcp.<host>` needs (RFC 6698 section 3); BL-4331 adds it. Hooks confirmed present: `ICertificateCheckHook` and `ExtensionCertificateCheckHookCatalogue` (ADR-1016), `IDatagramConnector`/`UdpDatagramConnector`, Ed25519/Ed448 in `Curl.Cryptography.UnitLibrary` (an allowed shared layer). What is left here once they are Done: the option (`--dane`, its resolver choice), the hook and its activator, the catalogue lines (option, description, certificate check hook - shared list files, added to `touches`), the ADR, the library's full CLAUDE.md, `--extensions`/`--ai-help extensions`, and the quality run.
- 2026-10-30 (lane 8): `--dane <resolver>` (host, host:port, a bare IP address or [IPv6]:port, port 53 by default) activates `DaneCheckHook`. It looks up `_<port>._tcp.<host>` through `TlsaLookup` and refuses (exit 60) when the records are bogus, when no usable answer comes, or when secure usable records match nothing. An insecure zone, a proved absence, no usable records or an IP-address host let the chain stand; an unknown port refuses. Decisions are in ADR-1155 (ADR-1149 to 1152 were already taken on the shared branch). Five seconds per query: the common resolver default.
- Registration: no URL scheme, so no line in `ExtensionCatalogue.cs`, which holds only handler factories (BL-4152 did the same). One line each in `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionCertificateCheckHookCatalogue.cs`. `Curl.slnx` already held `/Extensions/Dane/` from BL-4333. `Curl.Extension.Dane.UnitTests` now references Composition, as the CertificateTransparency tests do, to test the catalogues, `CURL_EXTENSIONS=0` and the listing.
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary,Curl.Extension.Composition.UnitLibrary` run took about 75 minutes because it reaches every host test project. It gave Composition 100/100 and Dane three members outside a gate. After the fix, Dane was re-scored with `-SkipTestRun` from `Curl.Extension.Dane.UnitTests`' own coverage alone, a lower bound: 100% line, 100% branch, complexity at most 10, worst CRAP 10.
- CI on Linux and macOS: the new tests use no paths, no OS errors and no platform certificates (the ECDSA certificate is made in memory), so they do not depend on the platform. On Windows the whole solution built clean and all 108 fast test projects passed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: aeadf7b1 b8ef90ec 647040e9 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size before any code: waits on BL-4333, BL-4334, BL-4335, BL-4331 and BL-4332 (codec, TLSA matching, DNSSEC verification, port in the certificate check hook, validating lookup)
- 2026-10-30: Backlog -> Doing. Criteria at claim: aeadf7b1 b8ef90ec 647040e9 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --dane <resolver> validates the TLSA RRset with DNSSEC from the root and refuses a certificate chain it does not match (exit 60)
