---
id: BL-5885
title: Fix fitness finding STD-204: Windows, non-self-signed --cacert anchor: chain errors are reported as "the certificate chain is incomplete", and two valid chains are refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.AnchoredChainFaults.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5885 — Fix fitness finding STD-204: Windows, non-self-signed --cacert anchor: chain errors are reported as "the certificate chain is incomplete", and two valid chains are refused

## Goal

STD-204 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows, non-self-signed --cacert anchor: chain errors are reported as "the certificate chain is incomplete", and two valid chains are refused. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed · **Area:** HTTPS path validation messages, Windows build.
- **Certificates:** intermediates under `root.pem`, each used as the only `--cacert` anchor: `noCertSign.pem` (keyUsage without keyCertSign), `caFalse.pem` (basicConstraints CA:FALSE), `pl0.pem` (pathLen 0 with a sub-CA below it), `noBC.pem` (no basicConstraints at all; valid for REF), `pl0sub.pem` (a sub-CA whose own parent has pathLen 0 — the violation is above the anchor). Leaves `p_eku_client` (EKU clientAuth only) and `p_unkcrit` (unknown critical extension) under `intA.pem`; anchors `intReqPol.pem` (requireExplicitPolicy) and `intInhAny.pem` (inhibitAnyPolicy).
- **Repro and expected (REF) vs actual (.NET):**

  | Command | REF | .NET |
  |---|---|---|
  | `curl -sS --ssl-no-revoke --cacert noCertSign.pem https://localhost:<p>/` | 60 `schannel: error 0x00000010` | 60 `the certificate chain is incomplete` |
  | `--cacert caFalse.pem` | 60 `error 0x00000400` | 60 incomplete |
  | `--cacert pl0.pem` | 60 `error 0x00000400` | 60 incomplete |
  | `p_eku_client` with `--cacert intA.pem` | 60 `error 0x00000010` | 60 incomplete |
  | `p_unkcrit` with `--cacert intA.pem` | 60 `error 0x08000100` | 60 incomplete |
  | `--cacert intReqPol.pem` / `intInhAny.pem` | 60 `error 0x02000000` | 60 incomplete |
  | `--cacert noBC.pem` | **0** | 60 incomplete |
  | `--cacert pl0sub.pem` | **0** | 60 incomplete |

- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs`, `CaCertificateFileAnchorsChain`: it requires every status other than "missing root" to be absent across the whole chain, including elements above the anchor, and maps every failure to "incomplete".
- **Acceptance check:** the `noBC` and `pl0sub` commands exit 0, and the `noCertSign` command prints `schannel: error 0x00000010`.

## Acceptance criteria

- [x] the `noBC` and `pl0sub` commands exit 0, and the `noCertSign` command prints `schannel: error 0x00000010`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-24: Added the new test file to `touches` (ADR-0482 needs it; no task in Doing names it).
- Done directly rather than through the /feature stages, to keep within the run's cost cap.
- Reproduced with a probe of `X509Chain` and fixed in `ChainErrors.cs`; decision in ADR-1347.
- Measured with `openssl s_server` against Schannel curl 8.23.0 (System32) and 8.21.0 (Git) and Curl:
  `noCertSign` 0x00000010, `caFalse` and `pl0` 0x00000400, `intReqPol` 0x02000000, `ekuclient`
  0x00000010, `unkcrit` 0x08000100, `noBC` and `pl0sub` (root sent or not) exit 0, all with
  `--ssl-no-revoke`; without it all report the revocation status unknown. Curl matched every one
  (the 8.23.0 `caFalse` run raced the server's start; 8.21.0 gave its answer).
- The reviewer's `intInhAny.pem` was not available: with the one built here both curls and Curl exit 0.
  Whatever fault .NET reports for theirs is now named by its value.
- Quality: the one failing member Measure-CodeQuality.ps1 reported (`WithBelowAnchorRevocation`, one
  of four branches, the revoked one) got `VerifyPeer_FaultAndTheLeafOnTheAnchorsList_IsTrustRevoked`,
  whose answer only that branch gives; the 31-minute measurement was not run again.
- The tests that read the platform's own chain statuses are Windows-only; the rest stub them.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9b4a630b 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Windows: a non-self-signed --cacert anchor names chain faults by Schannel's trust error and accepts noBC and pl0sub chains
