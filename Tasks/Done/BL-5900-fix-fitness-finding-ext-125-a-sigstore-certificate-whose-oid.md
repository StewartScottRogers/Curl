---
id: BL-5900
title: Fix fitness finding EXT-125: a sigstore certificate whose OIDC-issuer extension (1.3.6.1.4.1.57264.1.8) is not DER exits 2 with a raw .NET ASN.1 exception
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Sigstore.UnitLibrary/SigningCertificateIdentity.cs, Curl.Extension.Sigstore.UnitTests/SigningCertificateIdentityTests.cs, Curl.Extension.Sigstore.UnitTests/SigstoreFixture.cs, Curl.Extension.Sigstore.UnitTests/SigstoreVerificationTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
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

- [x] the repro exits 60 with a `--sigstore-bundle:` message about the issuer (or 26 naming the bundle) and no .NET exception text.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced on this branch by the new tests: all seven failed before the fix.
- Choice (within ADR-1101, so no new ADR): a `1.3.6.1.4.1.57264.1.8` value that is not exactly one DER
  `UTF8String` names no issuer, so with `--sigstore-issuer` the transfer exits 60 with
  `--sigstore-bundle: the bundle's certificate does not name the issuer <url>`. It does not fall back to the
  deprecated 1.1 extension: the 1.8 one is present and takes precedence. Without `--sigstore-issuer` the issuer
  is not read, as before.
- The same uncaught `AsnContentException` was reachable through the subject alternative name in the same file;
  a value that is not DER now names nothing (exit 60, "does not name the identity"). Tested on the raw bytes,
  not through a loaded certificate, because a platform's loader may refuse such a certificate.
- The exit 60 criterion is checked by unit test, not by running the command line: `FailureOf` gives the issuer
  message for a raw-bytes issuer, and `SigstoreTransferHook` turns any failure into exit 60 with the prefix.
- `touches` widened to the three test files in `Curl.Extension.Sigstore.UnitTests`; no task in Doing named them.
- Test files were first edited with a throwaway PowerShell script; a second such run was denied, and the
  remaining edits used the Edit tool.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.Sigstore.UnitLibrary`: 100% line, 100% branch, 0 failing
  members, worst CRAP 10.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 692aca4d 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. A sigstore certificate whose issuer or subject alternative name extension is not DER fails the check with exit 60, not a .NET exception
