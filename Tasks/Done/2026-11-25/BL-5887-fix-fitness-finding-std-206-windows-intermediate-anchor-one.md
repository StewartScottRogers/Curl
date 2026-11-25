---
id: BL-5887
title: Fix fitness finding STD-206: Windows, intermediate anchor: one distribution point with two URIs, the first a clean CRL and the second a revoking one, is refused as revoked
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5887 — Fix fitness finding STD-206: Windows, intermediate anchor: one distribution point with two URIs, the first a clean CRL and the second a revoking one, is refused as revoked

## Goal

STD-206 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows, intermediate anchor: one distribution point with two URIs, the first a clean CRL and the second a revoking one, is refused as revoked. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Probable (1 run plus code reading) · **Area:** HTTPS CRL revocation, Windows build.
- **Setup:** leaf `ia_cleanfirst_rev` under `intCRL.pem` with one DistributionPoint whose fullName lists `http://127.0.0.1:31080/c/clean_ia.crl` (valid, does not list the leaf) then `http://127.0.0.1:31080/c/two.crl` (lists the leaf).
- **Repro:** `curl -sS https://localhost:<port>/ --cacert intCRL.pem`
- **Expected (REF):** exit 0 — Windows uses the first URI that yields a valid CRL; with `--cacert root.pem` the port also exits 0.
- **Actual (.NET):** exit 60 `…has been revoked`.
- **Likely location:** `ServerCertificateVerification.AnchorRevocation.cs` `RevocationOf`: it fetches every URI of a point and ORs the results; the URIs of one point are alternatives for the same CRL.
- **Acceptance check:** the repro exits 0.

## Acceptance criteria

- [x] the repro exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- No longer reproduced on this branch: BL-5807 (STD-163, commit 5fd8d5a117), which landed after the reviewed commit
  1e5d591, made `ListRevocationOf` stop at the first usable CRL across every address of every point, so the
  behaviour needed no further change. No new ADR for the same reason.
- Verified end to end with a C# file-based app in a temporary directory (root -> intermediate used as `--cacert` ->
  leaf with one DistributionPoint naming two URIs; CRLs and TLS served on 127.0.0.1; fresh ports, CRL cache entries
  deleted afterwards). Clean list first: this Curl and Git for Windows curl 8.21.0 Schannel both exit 0 and fetch
  only the first URI. Control with the revoking list first: both exit 60 `... has been revoked`.
- BL-5807's tests built two points (`BuildCrlDistributionPointExtension` writes one point per URI), so the one-point
  shape was unpinned. Added `VerifyPeer_OnePointNamingACleanListThenARevokingList_AcceptsWithoutFetchingTheSecond`
  and added its file to `touches` (no other task in Doing names it). The only production change is the comment on
  `ListRevocationOf`, which now says it covers URIs of one point too.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line and branch, 0 failing members.
- The test stubs the fetch and chain status, so it is platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 079e799e 69116afe 3d836d5e.
- 2026-11-25: Doing -> Done. Already fixed by BL-5807; one-point two-URI case verified against real curl and pinned by a unit test
