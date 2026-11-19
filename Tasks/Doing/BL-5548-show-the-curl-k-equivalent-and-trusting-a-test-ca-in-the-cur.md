---
id: BL-5548
title: Show the curl -k equivalent and trusting a test CA in the Curl.Api README
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Api/README.md]
requirement: none
created: 2026-11-19
completed:
---
# BL-5548 — Show the curl -k equivalent and trusting a test CA in the Curl.Api README

## Goal

A reader of `Curl.Api/README.md` testing against a self-signed certificate finds, next to the other option samples, how to do what `curl -k` does and the safer way of trusting the test server's CA, without reading the source.

## Context

From bug report `2026-11-18T2025-Curl-bug-report.md` (finding 1, Low), found while writing Part 3 (Secure Servers) of the *Surly* cookbook. Build tested: `f4b30d981f`, `Curl.Api 0.2.0`, Windows 11. The port passed every Part 3 case; this is the only finding.

In `Curl.Api` the `curl -k` equivalent is `CurlRequest.Tls.Origin.VerifyPeer = false` and `VerifyHost = false`, two levels down. The README never shows it or names `Origin`. Its options section (around lines 69 and 281) lists `CurlTlsOptions` with `CurlTlsPeerOptions`, but a reader has to read the source to learn which property is the peer check and which is the host-name check.

Add a sample next to the other option samples, for example "Trust a test server: skip verification, or trust its CA", covering:

- `VerifyPeer = false` and `VerifyHost = false` on `Tls.Origin` (what `-k` does), with a one-line warning that it is for testing only;
- `CertificateAuthorityBlob`, the safer way, which the *Surly* recipe `https-serve-self-signed-in-process` uses.

Mark it as a sample `Curl.Api.UnitTests/Readme/ReadmeTests.cs` checks, as the other option samples are. Check each property name against the code before writing it.

Not a Curl defect, recorded in the report: `curl --negotiate` against a server offering only `Negotiate`, with no Kerberos, does not fall back to NTLM (`SEC_E_NO_CREDENTIALS`, exit 22); upstream curl 8.22.0 behaves the same. No work for it.

## Acceptance criteria

- [ ] `Curl.Api/README.md` has a sample beside the other option samples that sets `Tls.Origin.VerifyPeer = false` and `Tls.Origin.VerifyHost = false`, says it is the `curl -k` equivalent, and says it is for testing only.
- [ ] The same section shows trusting a test CA with `CertificateAuthorityBlob`, as the safer option.
- [ ] The README says which of `VerifyPeer` and `VerifyHost` is the peer check and which is the host-name check, and names `Origin` (and the proxy counterpart, if one exists).
- [ ] `Curl.Api.UnitTests/Readme/ReadmeTests.cs` passes and covers the new sample.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-18T2025-Curl-bug-report.md, finding 1.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 5b444aa8 57d2a932 10234ddc a4522cc9 3d836d5e.
