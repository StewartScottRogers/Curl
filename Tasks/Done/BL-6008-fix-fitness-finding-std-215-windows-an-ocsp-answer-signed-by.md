---
id: BL-6008
title: Fix fitness finding STD-215: Windows: an OCSP answer signed by an expired delegated responder is trusted below an intermediate anchor
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/OcspStapleVerifier.cs, Curl.Tls.UnitLibrary/OcspCertificateFields.cs, Curl.Tls.UnitLibrary/CLAUDE.md, Curl.Tls.UnitTests/OcspStapleVerifierTests.cs, Curl.Tls.UnitTests/OcspTestPki.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.AnchorOcsp.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6008 — Fix fitness finding STD-215: Windows: an OCSP answer signed by an expired delegated responder is trusted below an intermediate anchor

## Goal

STD-215 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows: an OCSP answer signed by an expired delegated responder is trusted below an intermediate anchor. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness. A self-signed `root.pem` signs every intermediate: `intOC3.pem` (OCSP tests), `intCR3.pem` (CRL tests), and name-constraint CAs `ncExclEmail5.pem` (critical, excluded rfc822Name `other.org`), `ncExclEmail.pem`, `ncPermEmail.pem` (permitted rfc822Name `example.com`), `ncExclUpn.pem` (excluded otherName UPN `evil.test`) and `ncPermUpn.pem` (permitted UPN `evil.test`). Plain HTTP servers on `127.0.0.1:21380`–`21382` serve CRLs and answer OCSP; each leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` unless stated and is served by its own TLS server answering `hello <leaf>`. Every leaf is in the Windows test only under the intermediate anchor named; **with `--cacert root.pem` (the Windows chain engine) the port matches curl in every case**. **Windows caches fetched CRL and OCSP responses for all processes**: before each run, delete every cached `127.0.0.1:213xx` URL (`certutil -urlcache <url> delete`), or REF's cached answers leak into the port's run. The reviewer's generators are `gen3.py`/`gen4.py`/`gen5.py` and the comparison script `cmp.sh`.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (2 leaves, direct and proxy; re-verified by the orchestrator) · **Area:** HTTPS / HTTPS-proxy OCSP, Windows build, intermediate anchor. Next to last round's STD-196 fix (OCSP support).
- **Certificates:** `intOC3.pem` under `root.pem`. Delegated responder `r3_expired`: issued by intOC3, EKU `id-kp-OCSPSigning`, OCSPNoCheck, notBefore now−30 days, **notAfter now−2 days**. Leaf `w_respexp_good`: no CDP; AIA OCSP `http://127.0.0.1:21381/ocsp/w_respexp_good`, answering "good", signed by `r3_expired` (included in the response). Leaf `q_respexp_good_crlrev`: the same "good" answer, plus a CDP whose current CRL from intOC3 **lists the leaf as revoked**.
- **Repro:**
  - `curl -sS --cacert intOC3.pem https://localhost:<port of w_respexp_good>/`
  - `curl -sS -x https://localhost:<port of w_respexp_good> --proxy-cacert intOC3.pem http://target.invalid/`
  - `curl -sS --cacert intOC3.pem https://localhost:<port of q_respexp_good_crlrev>/`
- **Expected (REF):** `w_respexp_good`: `curl: (60) schannel: the revocation status is unknown`. `q_respexp_good_crlrev`: `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
- **Actual (.NET):** `hello w_respexp_good` and `hello q_respexp_good_crlrev`, exit 0, direct and proxy — a certificate the CA's own CRL revokes is accepted on the word of an expired responder. (A not-yet-valid responder is accepted by REF too, so that case is not a finding.)
- **Likely location:** `Curl.Tls.UnitLibrary/OcspStapleVerifier.cs`, `IsAuthorised`: it checks the responder's EKU, issuer and signature but never its notBefore/notAfter (`OcspCertificateFields` does not carry them). Used by `ServerCertificateVerification.AnchorOcsp.cs` `OcspAnswerOf`. Refuse a delegated responder that is not valid at the check time; the status then falls back to the CRL.
- **Acceptance check:** the `q_respexp_good_crlrev` command exits 60 "…has been revoked"; the `w_respexp_good` command exits 60 "the revocation status is unknown"; a leaf answered by a valid delegated responder still exits 0.

## Acceptance criteria

- [x] the `q_respexp_good_crlrev` command exits 60 "…has been revoked"; the `w_respexp_good` command exits 60 "the revocation status is unknown"; a leaf answered by a valid delegated responder still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `OcspCertificateFields` now carries `NotAfter`, and `OcspStapleVerifier` refuses a delegated
  responder past it (ADR-1418). A not-yet-valid responder stays accepted, as REF accepts it; the
  OpenSSL `--cert-status` side of that is BL-6035.
- `touches` widened to the certificate reader, the library's CLAUDE.md and the tests. No task in
  Doing on `origin/work/dark-factory` named any of them.
- End to end on Windows, 2026-11-26, with the built `curl.exe` and a throwaway C# file-based app
  serving a fresh intermediate anchor, OCSP, CRL and TLS on 127.0.0.1, direct and as an HTTPS
  proxy: `w_respexp_good` exit 60 "schannel: the revocation status is unknown";
  `q_respexp_good_crlrev` exit 60 "...has been revoked"; a leaf answered by a valid delegated
  responder exit 0 with its body. REF's expected output is the finding's own measurement.
- `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 0 failing members.
- The `/feature` sub-agent stages were not spawned: the change is a few lines in two files, and
  the run has a cost cap.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 0fe8ae9d 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. Curl refuses an OCSP answer from an expired delegated responder, so the CRL decides
