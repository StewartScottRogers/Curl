---
id: BL-1043
title: Report libssh2's Unable to open public key file and No signing signature matched as the publickey denial reason
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1043 — Report libssh2's Unable to open public key file and No signing signature matched as the publickey denial reason

## Goal

`-v` on an `scp`/`sftp` transfer writes `SSH: publickey authentication denied: Unable to open public key file` when `--pubkey` cannot be read, and `... denied: No signing signature matched` when an RSA key finds no signature algorithm in the server's `server-sig-algs`, on both reference builds, instead of `Reason unknown (-1)`.

## Context

- Found by BL-990 (ADR-0281). `SshUserAuthentication.DenyPublicKeyAsync` and `UnreadablePublicKeyReasonAsync` still give `SshInfoLines.ReasonUnknown` for both cases.
- curl 8.21.0 `lib/vssh/libssh2.c` writes `Reason unknown (-1)` only when libssh2 returns `-1`; otherwise `libssh2_session_last_error`'s text.
- libssh2 1.11.1 `src/userauth.c`: `file_read_publickey` fails with `Unable to open public key file` (other texts for a malformed file: read the function), backend-independent; `_libssh2_key_sign_algorithm` fails with `LIBSSH2_ERROR_METHOD_NONE`, `No signing signature matched`.
- Windows measurements already on record: ADR-0230's table (`--pubkey` missing -> `Unable to open public key file`; `server-sig-algs` listing `ssh-ed25519` only -> `No signing signature matched`) and BL-568's Notes.
- Watch ADR-0271: after `No signing signature matched`, libssh2 keeps the method for the agent identities (`leftoverMethod`).

## Acceptance criteria

- [x] `Curl.Protocol.Ssh.UnitTests` pin `Unable to open public key file` for a missing `--pubkey` and `No signing signature matched` for an RSA key against `server-sig-algs` naming no RSA algorithm, on the WinCNG and OpenSSL presets.
- [x] A malformed (present but unparseable) `--pubkey` gives the text `file_read_publickey` gives for it, read from libssh2 1.11.1 and copied into Notes.
- [x] ADR-0262 point 5 and ADR-0281 are amended to match.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- libssh2 1.11.1 `src/userauth.c` `file_read_publickey`, read at tag `libssh2-1.11.1`, fails
  with `LIBSSH2_ERROR_FILE` and these texts, in order:
  - `fopen` fails: `Unable to open public key file`;
  - first line (up to the first `\r` or `\n`) one byte or shorter: `Invalid data in public key file`;
  - `fread` short: `Unable to read public key from file` (no path to it here: the file is read whole);
  - (`Unable to allocate memory for public key data`, `LIBSSH2_ERROR_ALLOC`, not reproducible);
  - nothing left after trimming trailing `isspace`: `Missing public key data`;
  - no space: `Invalid public key data`;
  - `_libssh2_base64_decode` fails (a lone leftover alphabet character): `Invalid key data, not base64 encoded`.
- `userauth_publickey_fromfile` returns that code as it is, so curl writes the text (not
  `Reason unknown (-1)`); the same on WinCNG and OpenSSL. The task's "malformed `--pubkey`"
  test uses a line with no space, `Invalid public key data`; `SshPublicKeyFileTests` pins every text.
- Two exactness fixes in `SshPublicKeyFile`, both from the source above: trailing white space
  is C's `isspace` set (space, tab, `\v`, `\f`), not .NET's wider one; and key data with no
  base64 characters (`ssh-rsa  comment`) decodes to an empty blob, which libssh2 accepts and
  sends, where this library used to refuse it.
- Design: `SshUserKeySource.ReadPublicKeyAsync` now returns `SshPublicKeyReading` (key, or
  libssh2's reason for a `--pubkey` failure); a missing reason means the key was to be
  derived from the private key, whose reason stays ADR-0281's backend choice.
- `No signing signature matched` leaves `leftoverMethod` as ADR-0271 has it; only the text changed.
- No new ADR: ADR-0262 point 5 and ADR-0281 amended in place.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v names libssh2's reason for an unopened or malformed --pubkey and for No signing signature matched, on WinCNG and OpenSSL
