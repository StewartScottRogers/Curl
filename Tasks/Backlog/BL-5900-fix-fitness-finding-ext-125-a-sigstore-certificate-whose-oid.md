---
id: BL-5900
title: Fix fitness finding EXT-125: a sigstore certificate whose OIDC-issuer extension (1.3.6.1.4.1.57264.1.8) is not DER exits 2 with a raw .NET ASN.1 exception
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Sigstore.UnitLibrary/SigningCertificateIdentity.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5900 — Fix fitness finding EXT-125: a sigstore certificate whose OIDC-issuer extension (1.3.6.1.4.1.57264.1.8) is not DER exits 2 with a raw .NET ASN.1 exception

## Goal

EXT-125 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: a sigstore certificate whose OIDC-issuer extension (1.3.6.1.4.1.57264.1.8) is not DER exits 2 with a raw .NET ASN.1 exception. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (3 runs) · **Area:** `--sigstore-*`, Windows build.
- **Setup:** a sigstore bundle `b_good.json` for `small.bin` whose signing certificate carries extension 1.3.6.1.4.1.57264.1.8 as the raw bytes `https://issuer.example` instead of a DER UTF8String; its root is `root.pem`. Serve `small.bin` on `http://127.0.0.1:37200/small.bin`.
- **Repro:** `curl -sS --sigstore-bundle b_good.json --sigstore-identity signer@example.com --sigstore-issuer https://issuer.example --sigstore-root root.pem http://127.0.0.1:37200/small.bin -o s.out`
- **Expected (ADR-1101):** a failed check exits 60 with a `--sigstore-bundle:` message; an unreadable bundle exits 26.
- **Actual (.NET):** `curl: (2) The provided data is tagged with 'Application' class value '8', but it should have been 'Universal' class value '12'.`, exit 2. Nothing is written (safe). The same value DER-encoded verifies; the deprecated raw-string 1.1 OID verifies.
- **Likely location:** `Curl.Extension.Sigstore.UnitLibrary/SigningCertificateIdentity.cs`: the `AsnReader` exception is not caught.
- **Acceptance check:** the repro exits 60 with a `--sigstore-bundle:` message about the issuer (or 26 naming the bundle) and no .NET exception text.

## Acceptance criteria

- [ ] the repro exits 60 with a `--sigstore-bundle:` message about the issuer (or 26 naming the bundle) and no .NET exception text.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
