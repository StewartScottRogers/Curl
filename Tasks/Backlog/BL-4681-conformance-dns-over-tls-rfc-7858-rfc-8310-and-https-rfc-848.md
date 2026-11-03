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
completed:
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

- [ ] `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      RFC 7858 and RFC 8484 (and RFC 8310 where RFC 7858's profile rules bind a client) that binds a client a
      `DnsQuery:` ID mapped to a test or to `n/a: <reason>`, leaving a section for RFC 9250 (BL-4683); the harness's
      matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: DoT on port 853 with the length prefix, a refused certificate (and
      `-k`), DoH GET and POST bodies and media types, a DoH non-2xx status, and the default port of DoT and DoH.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

- Filed by BL-4474's split (2026-11-03).

## Log

- 2026-11-03: Created.
