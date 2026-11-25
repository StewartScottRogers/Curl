---
id: BL-5884
title: Fix fitness finding STD-203: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5926]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/SchannelCaCertificateFile.cs, Curl.Networking.UnitLibrary/SchannelCaCertificateFileException.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/SchannelCaCertificateFileTests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5884 — Fix fitness finding STD-203: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77

## Goal

STD-203 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs, direct and proxy) · **Area:** HTTPS CA file parsing, Windows build.
- **Files** (from the shared PKI's `root.pem`, which signs the server's leaf):
  - `rootbad64.pem`: `root.pem` followed by a `-----BEGIN CERTIFICATE-----` block whose body is `!!!!notbase64!!!!`
  - `roottrunc.pem`: `root.pem` followed by a lone `-----BEGIN CERTIFICATE-----` line
  - `rootgarb.pem`: `root.pem` followed by a CERTIFICATE block whose base64 body is `AAAA`
- **Repro:** `curl -sS --ssl-no-revoke --cacert <file> https://localhost:<port>/` (and the `-x https://… --proxy-cacert <file>` form).
- **Expected (REF):** `rootbad64`: `curl: (77) schannel: failed to extract certificate from CA file 'rootbad64.pem': Cannot find the requested object.`; `roottrunc`: `curl: (77) schannel: CA file 'roottrunc.pem' is not correctly formatted`; `rootgarb`: exit 77 with the same text as `rootbad64`.
- **Actual (.NET):** `rootbad64` and `roottrunc`: exit 0, the root is trusted. `rootgarb` (and the same block placed before the root): exit 60 `the certificate chain is incomplete` — the root before the bad block is discarded, despite the code comment "what was imported before it is kept".
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs`, `ReadCaCertificates` (Schannel branch) and `ImportParsableCertificates`.
- **Acceptance check:** all three files give exit 77 with REF's message text.

## Acceptance criteria

- [x] all three files give exit 77 with REF's message text.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Run of 2026-11-24 (lane 7): measured and designed, no code changed. Waiting on BL-5926,
which holds `ServerCertificateVerification.cs`; the fix must change its
`TrustAnchorsUnusable`, the only place exit 77's message is chosen.

Still reproduces by reading the code: `ReadCaCertificates`' Schannel branch calls
`ImportParsableCertificates`, which skips what `ImportFromPem` cannot read.

Measured curl 8.22.0 (x86_64-w64-mingw32, Schannel) with
`Record-CurlExchange.ps1 -Tls` and `-sS --ssl-no-revoke --cacert <file>`. "good" is a valid
self-signed certificate that did not sign the server's, so "accepted" shows as exit 60
untrusted root. B and E are the BEGIN and END CERTIFICATE markers.

- Exit 77 `schannel: CA file '<path>' is not correctly formatted`: a B with no later
  `\n` + E (lone B line, also after a good block; CR-only line endings).
- Exit 77 `schannel: failed to extract certificate from CA file '<path>': An error occurred
  during encode or decode operation.`: the body between the markers is empty or only whitespace.
- Exit 77 `schannel: failed to extract certificate from CA file '<path>': Cannot find the
  requested object.`: a non-empty body that yields no certificate: `AAAA`, `AA`, `AAAAA`,
  `AA==AAAA`, `!`, `!` before or inside the base64, `-` inside it, a `Proc-Type:` header
  line, truncated base64; also a bad block before a good one, and a bad block followed by a lone B.
- Accepted: missing `=` padding; spaces, tabs and blank lines in the body; base64 on the B
  line; CRLF; text before B or after E on their lines; extra bytes after the DER; any text
  after the `=` padding (`!!!!` or `AAAA`); `<b64>-----END CERTIFICATE-----` + a further B,
  base64 and `\n` + E (the body ends at the first E, and scanning resumes after the `\n` + E).
- Empty file, or only a TRUSTED CERTIFICATE block: exit 60 untrusted root (no refusal).

Design (record it in an ADR, decided by Claude under Stewart's delegation):

- New `SchannelCaCertificateFile` reads the text as curl's `add_certs_data_to_store`
  (schannel_verify.c) does: each B in turn, requiring `\n` + E after it; body up to the first
  E; whitespace dropped; cut at the first `=`; empty is the encode/decode message; otherwise
  base64-decode (pad; drop a lone trailing character), cut the DER to its first encoded value
  (`AsnDecoder`, so Linux and macOS agree with Windows), `X509CertificateLoader.LoadCertificate`;
  any failure is "Cannot find the requested object.". It replaces `ImportParsableCertificates`.
- It throws `SchannelCaCertificateFileException : CryptographicException` carrying the message
  (built in `TlsFailureMessages`); `TrustAnchorsUnusable` returns exit 77 with it, as it
  picks out `CertificateRevocationListFileException` now.
- The path in the message is `CaCertificateFile` as given; for `CaCertificateBlob` curl's
  schannel_verify.c passes `(memory blob)` as the name (from source knowledge, not measured).
- Check `SslStreamTlsProviderTests` near `CorruptCertificatePem` (the "corrupt" data rows and
  the "mixed.pem" test) for Schannel-build expectations that this changes.

Run of 2026-11-25 (lane 1): implemented the design above, recorded in ADR-1394.
`SchannelCaCertificateFile` replaces `ImportParsableCertificates`; `SchannelCaCertificateFileException`
carries curl's line and `TrustAnchorsUnusable` returns exit 77 with it. Checked with the built
`curl.exe` through `Record-CurlExchange.ps1 -Tls`, with an unrelated self-signed root standing in for
the review's `root.pem` (alone it gives exit 60): `rootbad64` and `rootgarb` give exit 77 "Cannot find
the requested object." and `roottrunc` gives exit 77 "is not correctly formatted", REF's text.
`--proxy-cacert` uses the same `ServerCertificateVerification`, so it gets the same fix. Focused tests:
`SchannelCaCertificateFileTests` (every measured case; platform-neutral, with no paths and no OS text)
and `TlsFailureMessagesTests` for the two new lines. In `SslStreamTlsProviderTests` the Schannel
"corrupt", "undecodable" and "mixed.pem" cases now expect exit 77, and a blob case pins
`(memory blob)`. The change is platform-neutral, which meets the CI criterion for Linux and macOS.
`dotnet build` is clean and the fast tests pass in every test project. `Measure-CodeQuality.ps1 -Library
Curl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9e20ff72 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Waiting on BL-5926, which holds Curl.Networking.UnitLibrary/ServerCertificateVerification.cs (TrustAnchorsUnusable must change); curl measured and design recorded in Notes, no code changed
- 2026-11-25: Backlog -> Doing. Criteria at claim: 9e20ff72 69116afe 3d836d5e.
- 2026-11-25: Doing -> Done. Schannel build refuses a --cacert/--proxy-cacert file with a malformed CERTIFICATE block with exit 77 and curl's text
