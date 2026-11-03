---
id: BL-4681
title: Conformance: DNS over TLS (RFC 7858, RFC 8310) and HTTPS (RFC 8484) for dns:// (DnsQuery encrypted part 1 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4514, BL-4515, BL-4516]
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted, Documentation/Conformance/Extended/DnsQuery-Encrypted.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4681 — Conformance: DNS over TLS (RFC 7858, RFC 8310) and HTTPS (RFC 8484) for dns:// (DnsQuery encrypted part 1 of 3)

## Goal

Curl's `dns://` DNS over TLS and DNS over HTTPS transports (`--dns-transport tls` and `https`) are measured against
RFC 7858, RFC 8310 (where it binds a client) and RFC 8484 through the real curl command line, every client
requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Split from BL-4474 (part 1 of 3; BL-4682 and BL-4683 do DoQ). Read BL-4474's Context (in `Tasks/Deferred/`): the
harness, `[SpecRequirement]` IDs (`DnsQuery:RFC7858-<section>-<n>`, `DnsQuery:RFC8484-<section>-<n>`), the matrix
format and the `KnownFailures.txt` rule, `Documentation/Conformance/Extended/README.md`; measure against the
specification, one file per case group, nothing added to `Harness/`.

Starting points from BL-4474's sizing pass: DoT needs no new server: `ReferenceRoute.Tls(853, server, certificate)`
with BL-4473's `DnsReferenceServer` (an `IReferenceServer` speaking the two-octet length prefix; it records the
transport as `Tcp`, so assert TLS from `CurlRunResult.Exchanges`). See `Harness/ReferenceCertificate.cs` and how
other suites (e.g. `Mqtt5-Transports`) run TLS with and without `-k`. DoH needs a small HTTP/1.1 (or HTTP/2)
responder over the TLS route in `DnsQuery/Encrypted/` that records the method, path, `dns=` parameter,
`Content-Type`, `Accept` and body, and answers with `DnsReferenceMessageWriter` bytes or a scripted status. Library:
`Curl.Extended.Protocol.DnsQuery.UnitLibrary` `Transport/Tls/DnsTlsTransport`, `Transport/Https/` (ADR-1140,
ADR-1143, ADR-1148). A defect in the library is listed in `KnownFailures.txt` with a filed High
`Fix DnsQuery conformance: <requirement>` task (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      RFC 7858 and RFC 8484 (and RFC 8310 where RFC 7858's profile rules bind a client) that binds a client a
      `DnsQuery:` ID mapped to a test or to `n/a: <reason>`, leaving a section for RFC 9250 (BL-4683); the harness's
      matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: DoT on port 853 with the length prefix, a refused certificate (and
      `-k`), DoH GET and POST bodies and media types, a DoH non-2xx status, and the default port of DoT and DoH.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

- Filed by BL-4474's split (2026-11-03).
- Added `DnsQuery/Encrypted/DohReferenceServer` (HTTP/1.1 DoH responder on a TLS route, records request line,
  headers and body; answers 200 `application/dns-message` or a scripted status), `DohReceivedRequest`, and
  `DnsTlsRefusalServer` (the `Mqtt5TlsRefusalServer` pattern, reusing the public `Mqtt5/Transports/DelegatingDuplexStream`
  rather than copying it). Cases: `DnsOverTlsCases` (853 default, TLS before DNS, length prefix; refused certificate
  exit 60 with no cleartext fallback; EDNS padding) and `DnsOverHttpsCases` (443 default, POST body, Content-Type,
  Accept, ID 0, 2xx SERVFAIL read as an answer, 503 exit 22, HTTP/2, refused certificate exit 60).
- Decision (sensible default): curl's DoH client sends POST only, so "DoH GET" is covered by asserting the request is
  a POST with no `dns=` parameter, the GET base64url row is `n/a` (RFC 8484 section 4.1 leaves the method to the
  client), and `DohReferenceServerTests` drives the server with a GET directly.
- Known failures filed: BL-4684 (DoH DNS ID 0, RFC 8484 4.1), BL-4685 (EDNS(0) Padding, RFC 8310 9), BL-4686
  (DoH over HTTP/2, RFC 8484 5.2). Every other case passes.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` 0 failing members (first run
  found `DohReferenceServer.ServeAsync` at complexity 12; split into `ReadUntilAsync`). `dotnet build Curl.slnx
  -warnaserror` clean; fast tests green (115 test assemblies passed). Tests are platform-neutral: no paths, the
  certificate is made in memory, the 60 cases assert only the `curl: (60) ` prefix and the 22 case normalises CRLF.
  CI on Linux and macOS is covered by keeping it platform-neutral and the fast tests green here.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 5453e041 b23c8ea5 f3c72898 d8e3fb0e 627fbee3 17248609.
- 2026-11-03: Doing -> Done. dns:// DoT and DoH measured against RFC 7858, 8310 and 8484 with 9 cases; 3 known failures filed as BL-4684..BL-4686
