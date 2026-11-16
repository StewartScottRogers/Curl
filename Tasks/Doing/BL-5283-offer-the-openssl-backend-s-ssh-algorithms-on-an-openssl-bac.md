---
id: BL-5283
title: Offer the OpenSSL backend's SSH algorithms on an OpenSSL-backed build
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Ssh.UnitLibrary/Negotiation/SshAlgorithmPreferences.cs, Curl.Ssh.UnitLibrary/PacketProtection/SshPacketProtections.cs, Curl.Ssh.UnitTests/Negotiation/SshAlgorithmPreferencesOpenSslTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5283 — Offer the OpenSSL backend's SSH algorithms on an OpenSSL-backed build

## Goal

The port's SSH client offers the key exchange, host key, cipher and MAC lists libssh2 1.11.1 offers on OpenSSL (Curve25519, ECDH, ECDSA, Ed25519, AES-GCM, and on request Blowfish, CAST-128 and RIPEMD-160), where it now offers only the WinCNG backend's lists.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 5 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ssh.md`, SSH-1. Related: BL-5279 (handoff entry 1).
- **Surl tests (5):** `CurlPortBesideUpstreamCurlOverSshTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms`, `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[blowfish-cbc]` and `[cast128-cbc]`, `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160]` and `[hmac-ripemd160@openssh.com]`; Surl commit `4c5010959a6096d5ee8bfcdfbef890e9d6160653`.
- **Command lines:** `curl -sS -k sftp://127.0.0.1:<port>/x` against a `KEXINIT` recorder; and `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt` against `surl sftp://... --throwaway-hostkey --user tester:secret --allow-weak-ssh-algorithms` with `--ssh-ciphers blowfish-cbc`, `--ssh-ciphers cast128-cbc` (curl run with `OPENSSL_CONF` activating the `legacy` provider), or `--ssh-ciphers aes128-ctr --ssh-macs hmac-ripemd160` (or `...@openssh.com`).
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`: `KEXINIT` kex list starts `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,...`; host keys include ECDSA and `ssh-ed25519`; ciphers include `aes256-gcm@openssh.com`, `blowfish-cbc`, `cast128-cbc`; MACs include `hmac-ripemd160`. Each weak-algorithm download exits 0 with `hello world\n`.
- **Port (defect):** `KEXINIT` differs from line 2: exactly the WinCNG lists (RSA host keys only; no GCM, Blowfish, CAST-128 or RIPEMD-160). Each download exits 2, `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`. (Against the Schannel/WinCNG reference build, `KexInit_WindowsBuild_ListsTheWinCngAlgorithms` agrees.)
- **What decides:** libssh2 1.11.1 `src/crypt.c`, `src/mac.c`, `src/kex.c`, `src/hostkey.c` (OpenSSL entries under `LIBSSH2_OPENSSL`); RFC 4253 section 6.3, RFC 5656, RFC 8731, RFC 8709.
- **Where to start:** `Curl.Ssh.UnitLibrary/Negotiation/SshAlgorithmPreferences.cs` (`WindowsReference` preset), `Curl.Ssh.UnitLibrary/PacketProtection/SshPacketProtections.cs`.

## Acceptance criteria

- [ ] The port's `KEXINIT` on the build it pairs with upstream's OpenSSL build lists the OpenSSL backend's name-lists in libssh2 1.11.1's order, shown by a port test; or a port ADR records that the port keeps the WinCNG lists.
- [ ] Against a server offering only `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` or `hmac-ripemd160@openssh.com`, the port completes the SFTP download (exit 0), shown by port tests.

## Notes

The port follows the Windows Schannel/WinCNG reference build on Windows on purpose (its CLAUDE.md "Decisions"); whether it follows the OpenSSL build here is its maintainer's call, recorded in an ADR under Stewart's delegation. Shares `SshAlgorithmPreferences.cs` with BL-5279, so the two run one after the other.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 3b1ce6e2 37344a61.
