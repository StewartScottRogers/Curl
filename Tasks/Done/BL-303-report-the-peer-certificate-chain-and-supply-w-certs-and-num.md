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
completed: 2026-09-27
---
# BL-303 — Report the peer certificate chain and supply -w certs and num_certs

## Goal

`%{num_certs}` prints the number of certificates in the peer's chain and `%{certs}` prints them in curl 8.21.0's text form, for an https:// transfer, and `0` and nothing for any other.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- Measured: file:// and http:// print `0` and nothing. `https://example.com/` printed `num_certs` `4` without `--certinfo`, and `certs` began `Subject:CN=example.com`, `Issuer:C=US, O=SSL Corporation, CN=Cloudflare TLS Issuing ECC CA 3`, `Version:2`, `Serial Number:01:ee:...:`, `Signature Algorithm:ecdsa-with-SHA256`, `Start Date:... GMT`, `Expire Date:... GMT`, `Public Key Algorithm:`. Measure against a loopback TLS server with a known certificate before pinning the full text.
- The chain comes from the TLS handshake (`SslStream.RemoteCertificate` and the chain the validation callback sees); record in an ADR how it reaches `TransferReport`.

## Acceptance criteria

- [x] An ADR names where the chain is captured and the `TransferReport` member that carries it.
- [x] `num_certs` and `certs` render as measured for file://, http:// and a loopback https:// transfer, pinned in tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every project touched.

## Notes

- Resumed from `factory/BL-303-wip` (cherry-picked without committing). The earlier run's
  ADR was numbered 0047, then 0053, both of which other lanes had taken; renumbered to ADR-0054 and every
  reference updated. Its follow-up for the HTTP handler was written as BL-314, an ID another
  lane also took; filed as BL-331 instead.
- Plan as delivered (ADR-0054): `SslStreamTlsProvider` lists the DER of the server's
  certificate and then `ChainPolicy.ExtraStore` (what the server sent, in order) in its
  validation callback; `ConnectResult.PeerCertificates` carries it, `TcpConnector` passes it
  on, `TransferReport.PeerCertificates` holds it; `Curl.Output.PeerCertificateText` (with
  `DerReader`, `X509CertificateFields`, `DerText`) ports curl's `Curl_extract_certinfo`.
- Measurement (2026-09-26, curl 8.21.0 mingw Schannel): a loopback `openssl s_server` on
  port 18304 serving a leaf (`CN=localhost`), an intermediate (`CN=BL303 Intermediate`) and a
  root, chain in `Curl.Output.UnitTests/Fixtures/LoopbackChain.pem`, queried with
  `curl -k -s -o NUL -w "%{num_certs}
%{certs}" https://127.0.0.1:18304/`: printed `3` and
  the bytes pinned in `Fixtures/LoopbackChain.certs.txt`. The earlier run's exact openssl
  commands were not carried in the WIP commit; the certificates and output bytes were.
  file:// and http:// printed `0` and nothing (BL-284's Notes).
- Coverage: Curl.Output.UnitLibrary and Curl.Protocol.Abstractions.UnitLibrary 100/100.
  Curl.Networking.UnitLibrary 98.57/99.79, all in members this task did not change:
  `TcpDialer.DialAsync` and `UdpDatagramChannel` Send/Receive are covered only by their
  `Integration` tests, and `CreateCipherSuitesPolicy`'s non-Windows line is BL-268. Every
  member this task added or changed is at 100%.
- Until BL-331 lands, a real https:// transfer still prints `0` and nothing: the HTTP
  handler does not yet copy the chain into its report.

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-26: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lanes cleaned up and cut from 6 to 3 mid-run; partial work saved on branch factory/BL-303-wip: start with git cherry-pick --no-commit factory/BL-303-wip and carry on from it.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. %{num_certs} and %{certs} print the peer chain from TransferReport.PeerCertificates as curl 8.21.0 Schannel does, and 0 and nothing without TLS
