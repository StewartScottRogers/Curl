---
id: BL-4333
title: Extension: Scaffold the Dane extension and code DNSSEC-aware DNS messages
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary/Curl.Extension.Dane.UnitLibrary.csproj, Curl.Extension.Dane.UnitLibrary/CLAUDE.md, Curl.Extension.Dane.UnitLibrary/Wire, Curl.Extension.Dane.UnitTests/Curl.Extension.Dane.UnitTests.csproj, Curl.Extension.Dane.UnitTests/Wire, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4333 — Extension: Scaffold the Dane extension and code DNSSEC-aware DNS messages

## Goal

`Curl.Extension.Dane.UnitLibrary` exists and encodes DNSSEC-aware DNS queries and decodes the resource records DANE and DNSSEC need, in its `Wire/` folder.

## Context

First of the split of BL-4151 (DNSSEC and DANE TLSA extension, ADR-0999). Create the library and its `.UnitTests` twin with the `new-project` skill, in `/Extensions/Dane/` in `Curl.slnx` (one single-line `<Folder>` under the extensions marker comment). It references `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Extension.Abstractions.UnitLibrary`, `Curl.Networking.UnitLibrary` and `Curl.Cryptography.UnitLibrary` (Ed25519/Ed448 for BL-4335), with `InternalsVisibleTo` its tests, so the later subtasks only add files in their own folders. `Curl.Extension.EncryptedDns.UnitLibrary/Wire` has an A/AAAA codec to read for style, but an extension never references another extension: this codec is its own. No catalogue line and no option yet (BL-4151 adds them).

`Wire/`: a query encoder for any QTYPE with an EDNS0 OPT record (RFC 6891 section 6.1.2) with the DO bit set (RFC 4035 section 3.2.1) and optionally CD; a response decoder giving the header (ID, TC, AD, CD, RCODE) and the answer and authority records, owner names decompressed (RFC 1035 section 4.1.4), with typed RDATA for TLSA (RFC 6698 section 2.1), DNSKEY (RFC 4034 section 2.1), RRSIG (section 3.1), DS (section 5.1), NSEC (section 4.1), NSEC3 (RFC 5155 section 3.2) and CNAME, and the raw RDATA of each record kept for canonical-form signing (RFC 4034 section 6).

## Acceptance criteria

- [x] `Curl.Extension.Dane.UnitLibrary` and `Curl.Extension.Dane.UnitTests` exist in `/Extensions/Dane/` in `Curl.slnx`, with the four project references above, and the build's `VerifyExtensionIsolation` passes.
- [x] Unit tests check the query encoder against RFC 6891 section 6.1.2 and RFC 4035 section 3.2.1 (OPT record, DO bit) with recorded bytes.
- [x] Unit tests decode each record type above from recorded bytes, named after its RFC section, and a malformed or truncated message gives a decode failure, never an exception escaping the decoder.
- [x] `Curl.Extension.Dane.UnitLibrary/CLAUDE.md` describes the library and its `Wire/` folder.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size before any code. BL-4334, BL-4335 and BL-4332 add their own folders; BL-4151 adds the option, hook, catalogue lines and the full CLAUDE.md.
- 2026-10-29 (lane 2): Built by hand rather than with `dotnet new`, copying EncryptedDns's csproj shape (no template Class1). Decisions in ADR-1120: the OPT record advertises 1232 octets (DNS flag day 2020); `DnssecResponseDecoder.TryDecode` returns false for anything malformed or a query (QR clear) and never throws; the ID is the caller's to match; the additional section is not read; pointers are followed only backwards; RRSIG signer and NSEC next names are read from the RDATA alone, so a pointer there is malformed (RFC 4034); `RawData` is the RDATA as received. The RDATA test bytes come from the RFC examples (RFC 6698 2.3, RFC 4034 2.3/3.3/4.3/5.3, RFC 5155 appendix A), with keys, signatures and the NSEC3 hash shortened to their leading octets. Quality: 100% line and branch coverage, max complexity 10, CRAP 10 (after splitting three methods that were at 12 and 13). 31 tests in Curl.Extension.Dane.UnitTests; full build clean and fast tests green on Windows; the tests touch no file system, path, OS error or certificate, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 07b6e3f8 c5a2fcae 57817966 aa542e0e 6db41805 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.Dane.UnitLibrary exists with a DNSSEC-aware query encoder (EDNS0 OPT, DO, CD) and a response decoder typing TLSA, DNSKEY, RRSIG, DS, NSEC, NSEC3 and CNAME
