---
id: BL-915
title: Pass the HTTPS server certificate's tls-server-end-point bindings to the hand-built Kerberos Negotiate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-832]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0234-negotiate-carries-the-https-server-certificate-to-the-hand-built-kerberos-for-tls-server-end-point-bindings.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-915 — Pass the HTTPS server certificate's tls-server-end-point bindings to the hand-built Kerberos Negotiate

## Goal

`HandBuiltKerberosSecurityContext` sets `KerberosGssContextOptions.ChannelBindings` to `tls-server-end-point:` followed by the HTTPS server certificate's RFC 5929 hash (SHA-256 for an MD5 or SHA-1 signature, else the signature's own hash), so hand-built Negotiate over HTTPS sends the bindings curl 8.18.0 with MIT was measured sending (BL-832, ADR-0171).

## Context

- BL-832 measured curl 8.18.0 with MIT krb5 1.22.1 over HTTPS: its authenticator's `Bnd` is the MD5 of RFC 2744's structure with no addresses and application data `tls-server-end-point:` + SHA-256 of the sha256RSA server certificate. `Curl.Kerberos.UnitLibrary` now takes that application data (`KerberosGssContextOptions.ChannelBindings`).
- `Curl.Authentication.UnitLibrary/HandBuiltKerberosSecurityContext.cs` builds the options today without it. The server certificate has to reach it through the security-context request; plain HTTP passes none (zeros), as curl does.
- If the certificate cannot reach the request without changing another project, add that project to `touches` per the board's rules.

## Acceptance criteria

- [x] A `Curl.Authentication.UnitTests` test pins the options' `ChannelBindings` for a known certificate (a sha256RSA one gives `tls-server-end-point:` + its SHA-256).
- [x] A test pins that a request without a server certificate (plain HTTP) passes `null`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-832.
- Touches widened: the certificate can only reach the context through `SecurityContextRequest`
  and `HttpAuthRequest`, both in `Curl.Protocol.Abstractions.UnitLibrary`, which no task in
  Doing named; so it and its tests were added, with the new ADR-0234 and the ADR index.
  Filling `HttpAuthRequest.ServerCertificate` in `Curl.Protocol.Http.UnitLibrary` (held by
  BL-624) is BL-966.
- Carried as `ReadOnlyMemory<byte>` DER, the form `ConnectResult.PeerCertificates` uses;
  empty for none.
- `TlsServerEndPointChannelBindings.Of` maps signature OIDs to RFC 5929 hashes in a table,
  so the OID list adds no branches; unknown algorithms (PSS, Ed25519, SHA-224) give no
  bindings provisionally, ADR-0234; BL-965 measures curl there and matches it.
- Tests: `TlsServerEndPointChannelBindingsTests` (sha256RSA, sha1RSA through a custom signature
  generator since `CertificateRequest` refuses SHA-1, ECDSA SHA-384, sha512RSA, PSS, none);
  `HandBuiltSecurityContextFactoryTests.ChannelBindings` pins the authenticator's `Bnd` end to
  end for Negotiate and Kerberos, and zeros without a certificate;
  `NegotiateHttpAuthenticatorTests.ContextRequestFor_ServerCertificate_PassesItForChannelBindings`.
- Measure-CodeQuality: Curl.Authentication.UnitLibrary 100% line, 100% branch, 0 failing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Hand-built Kerberos Negotiate sends tls-server-end-point channel bindings from SecurityContextRequest.ServerCertificate
