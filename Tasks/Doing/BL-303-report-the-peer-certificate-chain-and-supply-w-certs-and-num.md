---
id: BL-303
title: Report the peer certificate chain and supply -w certs and num_certs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-303 — Report the peer certificate chain and supply -w certs and num_certs

## Goal

`%{num_certs}` prints the number of certificates in the peer's chain and `%{certs}` prints them in curl 8.21.0's text form, for an https:// transfer, and `0` and nothing for any other.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- Measured: file:// and http:// print `0` and nothing. `https://example.com/` printed `num_certs` `4` without `--certinfo`, and `certs` began `Subject:CN=example.com`, `Issuer:C=US, O=SSL Corporation, CN=Cloudflare TLS Issuing ECC CA 3`, `Version:2`, `Serial Number:01:ee:...:`, `Signature Algorithm:ecdsa-with-SHA256`, `Start Date:... GMT`, `Expire Date:... GMT`, `Public Key Algorithm:`. Measure against a loopback TLS server with a known certificate before pinning the full text.
- The chain comes from the TLS handshake (`SslStream.RemoteCertificate` and the chain the validation callback sees); record in an ADR how it reaches `TransferReport`.

## Acceptance criteria

- [ ] An ADR names where the chain is captured and the `TransferReport` member that carries it.
- [ ] `num_certs` and `certs` render as measured for file://, http:// and a loopback https:// transfer, pinned in tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every project touched.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-26: Backlog -> Doing.
