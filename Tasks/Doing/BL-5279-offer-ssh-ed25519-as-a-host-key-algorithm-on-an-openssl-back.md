---
id: BL-5279
title: Offer ssh-ed25519 as a host key algorithm on an OpenSSL-backed build
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Ssh.UnitLibrary/Negotiation/SshAlgorithmPreferences.cs, Curl.Ssh.UnitTests/Negotiation/SshAlgorithmPreferencesEd25519HostKeyTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5279 — Offer ssh-ed25519 as a host key algorithm on an OpenSSL-backed build

## Goal

The port's SSH client lists `ssh-ed25519` in its `KEXINIT` host key algorithms, so against a server that holds only an Ed25519 host key it agrees the algorithm and then judges the key, as upstream curl 8.21.0 with libssh2 1.11.1 on OpenSSL does.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 1 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 1. Related: BL-5283 (handoff entry 5, the same backend algorithm list seen from the SSH family); both change `SshAlgorithmPreferences.cs`, so they run one after the other.
- **Surl test:** `CurlPortBesideUpstreamCurlOverHttpTests.Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60` (mirrors `UpstreamCurlTrustsSurlsHostCertificateTests`), Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command line:** `curl -sS --knownhosts <known_hosts> -u tester:secret sftp://127.0.0.1:<port>/a.txt` against `surl -v --hostkey <Ed25519 key> --hostcert <its certificate> sftp://127.0.0.1:0/`; `known_hosts` holds only an `@cert-authority [127.0.0.1]:<port>` line.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`: exit 60, `curl: (60) SSL peer certificate or SSH remote key was not OK`; the server notes `SSH negotiated kex curve25519-sha256, host key ssh-ed25519, ...`.
- **Port (defect):** exit 2, `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`; the server notes `SSH no common host key algorithm; client offered rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com`.
- **What decides:** RFC 8709 section 4 (`ssh-ed25519`); RFC 4253 section 7.1 (the client lists every host key algorithm it supports); libssh2 1.11.1 `src/hostkey.c` compiles `ssh-ed25519` in on OpenSSL.
- **Where to start:** `Curl.Ssh.UnitLibrary/Negotiation/SshAlgorithmPreferences.cs` (today `WindowsReference` sends the WinCNG lists on Windows).

## Acceptance criteria

- [ ] The port's SSH client offers `ssh-ed25519` among its host key algorithms on the build it pairs with upstream's OpenSSL build, shown by a port unit test of its `KEXINIT` name-list.
- [ ] Against a server holding only an Ed25519 host key not in `known_hosts`, the port exits 60 (`CURLE_PEER_FAILED_VERIFICATION`), shown by a port test.

## Notes

The port follows the Windows Schannel/WinCNG reference build on Windows on purpose (its CLAUDE.md "Decisions"). Upstream's OpenSSL build on Windows is a different upstream build, so this may call for an ADR deciding which preset applies where, recorded under Stewart's delegation.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 1c1930aa 5f9c22dc.
