---
id: BL-4205
title: Add Curl.Extension.EncryptedDns with a DNS message codec and a DNS-over-TLS resolver
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4096]
touches: [Curl.Extension.EncryptedDns.UnitLibrary/Curl.Extension.EncryptedDns.UnitLibrary.csproj, Curl.Extension.EncryptedDns.UnitLibrary/CLAUDE.md, Curl.Extension.EncryptedDns.UnitLibrary/Wire, Curl.Extension.EncryptedDns.UnitLibrary/DnsOverTls, Curl.Extension.EncryptedDns.UnitTests/Curl.Extension.EncryptedDns.UnitTests.csproj, Curl.Extension.EncryptedDns.UnitTests/Wire, Curl.Extension.EncryptedDns.UnitTests/DnsOverTls, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4205 — Add Curl.Extension.EncryptedDns with a DNS message codec and a DNS-over-TLS resolver

## Goal

`Curl.Extension.EncryptedDns.UnitLibrary` holds a DNS message codec and an `IDnsResolver` that looks host names up over DNS-over-TLS (RFC 7858), tested with fake connections.

## Context

Split from BL-4148 (extension 48 of 63; ADR-0999, CLAUDE.md "Extensions"). Create both projects with the `new-project` skill in `/Extensions/EncryptedDns/` in `Curl.slnx` (one single-line `<Folder>` under the marker comment). The extension references only `Curl.Protocol.Abstractions`, the shared layers and `Curl.Extension.Abstractions` (`VerifyExtensionIsolation`); the DoH codec in `Curl.Networking.UnitLibrary`'s `DohDnsResolver.*` is internal, so write the codec in `Wire/` (A and AAAA queries per RFC 1035 section 4, answers decoded with name compression, RCODE and truncation failures). The DoT resolver (`DnsOverTls/`) takes an injected `IConnector` and `ITlsProvider` (production ones come from `Curl.Networking.UnitLibrary`'s `TcpConnector` and TLS provider when BL-4148 wires the hook), connects to port 853 by default, and sends each query with the two-octet length prefix of RFC 7858 section 3.3 / RFC 1035 section 4.2.2, reading length-prefixed answers. Not registered in any catalogue here: BL-4148 adds the options, hook and catalogue lines.

## Acceptance criteria

- [ ] Both projects exist in `/Extensions/EncryptedDns/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] Unit tests check the codec against named sections of RFC 1035 (query header, question, answer with compression, RCODE) with recorded bytes.
- [ ] Unit tests check the DoT resolver against named sections of RFC 7858 (port 853, TLS before any query, length-prefixed messages, A and AAAA answers) over fake connections, no network.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.EncryptedDns.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4148's run, which split that task for size (its Notes ask for wire format, client flow and options separately).

## Log

- 2026-10-29: Created.
