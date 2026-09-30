---
id: BL-990
title: Report the OpenSSL build's publickey denial reason for an unreadable SSH key off Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-681]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-990 — Report the OpenSSL build's publickey denial reason for an unreadable SSH key off Windows

## Goal

On Linux and macOS, `-v` on an `scp`/`sftp` transfer whose `--key` cannot be read (missing passphrase, wrong passphrase, `chacha20-poly1305@openssh.com` key, unknown format) names libssh2's OpenSSL-backend reason instead of the WinCNG build's `Reason unknown (-1)`.

## Context

- Found by BL-681. With no `--pubkey`, libssh2 1.11.1 derives the public key from the private key file; the reason it gives when that fails depends on the cryptography backend.
- Windows reference (curl 8.21.0, libssh2 1.11.1 WinCNG, measured by BL-568): `SSH: publickey authentication denied: Reason unknown (-1)`, exit 67 `Authentication failure`.
- Ubuntu curl 8.18.0 on libssh2 1.11.1 OpenSSL 3.5.5, measured by BL-681 against OpenSSH 10.2 (`sftp`, `-u user:wrong`, `--key` an encrypted Ed25519 key with `--pass nope`, no `--pass`, or a `chacha20-poly1305@openssh.com` key): curl 8.18's wording `SSH public key authentication failed: Unable to extract public key from private key file: Wrong passphrase or invalid/unrecognized private key file format`, then exit 67 `Authentication failure`. curl 8.21.0's `lib/vssh/libssh2.c` writes the same libssh2 message after `SSH: publickey authentication denied: ` (ADR-0262).
- `SshAlgorithmPreferences.CryptographyBackend` already tells the two builds apart; `SshInfoLines.ReasonUnknown` is the line to make per backend.

## Acceptance criteria

- [x] Measured with the OpenSSL build of curl 8.21.0 (or its `lib/vssh/libssh2.c` read, if no 8.21.0 OpenSSL build is available) and copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the OpenSSL-backend reason for the OpenSSL preset and keep `Reason unknown (-1)` for the WinCNG preset.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Source read, not measured:** no curl 8.21.0 OpenSSL build was available (`Record-CurlExchange.ps1` runs the Windows curl only), so the sources were read at their tags on 2026-09-30:
  - curl `curl-8_21_0` `lib/vssh/libssh2.c` lines 1576-1586: `char unknown[] = "Reason unknown (-1)"; if(rc == -1) err_msg = unknown; else libssh2_session_last_error(...); infof(data, "SSH: publickey authentication denied: %s", err_msg);`
  - libssh2 `libssh2-1.11.1` `src/openssl.c` `_libssh2_pub_priv_keyfile`: `BIO_new_file` failing -> `LIBSSH2_ERROR_FILE`, `Unable to extract public key from private key file: Unable to open private key file`; neither `PEM_read_bio_PrivateKey` nor `_libssh2_pub_priv_openssh_keyfile` decoding it -> `LIBSSH2_ERROR_FILE`, `Unable to extract public key from private key file: Wrong passphrase or invalid/unrecognized private key file format` (BL-681 measured this text on curl 8.18.0); a PEM key of another type -> `... Unsupported private key file format` (not reachable here).
- **Decision (ADR-0278):** with no `--pubkey` and an underivable public key, the OpenSSL preset says whether the private key file opened; WinCNG and `Full` keep `Reason unknown (-1)`. The empty path used when no key is found is an unopened file, so the handler tests that run the OpenSSL preset with no key in `HOME` now expect `Unable to open private key file`.
- **Design:** `SshTransport.CryptographyBackend` exposes the preset's backend to `SshUserAuthentication`; `SshUserKeySource.PrivateKeyFileOpensAsync` probes the file; `SshAlgorithmPreferences.OpenSslBackend` names the constant.
- **touches:** added `Documentation/Planning/Decisions` for ADR-0278 and its README row; no task in Doing names it.
- **Follow-up filed:** BL-1040 - an unreadable `--pubkey` (`Unable to open public key file`) and an RSA key with no `server-sig-algs` match (`No signing signature matched`) still say `Reason unknown (-1)` on both builds, though ADR-0230 measured libssh2's texts on Windows.
- **Cases pinned on the OpenSSL preset:** no `--pass`, a wrong `--pass`, a `chacha20-poly1305@openssh.com` key with the right one (the reader refuses it, as libssh2 does), a file of no known format, a missing file; an unreadable `--pubkey` keeps `Reason unknown (-1)` (BL-1040). WinCNG keeps `Reason unknown (-1)`.
- **Results:** `Curl.Protocol.Ssh.UnitTests` 1412 passed (fast); solution fast tests all green; `dotnet build Curl.slnx -warnaserror` clean; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` 100% line, 100% branch, 798 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. With no --pubkey, an unreadable --key is refused with the OpenSSL build's libssh2 reason off Windows (Unable to open private key file / Wrong passphrase or invalid/unrecognized private key file format); WinCNG keeps Reason unknown (-1)
