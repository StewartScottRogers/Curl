# ADR-1370: SFTP and SCP keep downloading over `blowfish-cbc` and `cast128-cbc` where the macOS libssh2 build crashes

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5909 (Surl handoff `Port-Board-Handoff.md` entry 28, finding SSH-3)
- Decided by Claude under Stewart's delegation.

## Context

Curl is to match the platform's curl. On macOS, one measured upstream build crashes where Curl
completes the transfer.

The measurement is Surl's, not repeated here: osx-arm64, upstream curl 8.21.0, Surl's
`curl-port-phase-7` run 45412760425 (2026-11-24), Curl commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`.

Command: `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://…/a.txt` against
`surl --allow-weak-ssh-algorithms` offering only `blowfish-cbc`, and again offering only
`cast128-cbc`.

| | Negotiated | Then |
| --- | --- | --- |
| Upstream curl | `curve25519-sha256`, `rsa-sha2-512`, the cipher, `hmac-sha2-256` | exit 139 (SIGSEGV in its libssh2 build), empty output, no login reaches `surl` |
| Curl | the same | logs in, downloads the 12 bytes `hello world`, exit 0 |

Surl's `Ssh.md` reports that upstream's static OpenSSL build on Windows (SSH-1) downloads the file
over each of the two ciphers and exits 0, and that the linux-x64 comparison agrees with Curl on
those cases. The Windows reference build, whose libssh2 uses WinCNG, does not offer either cipher,
and Curl on Windows does not either (ADR-1278).

## Decision

Curl keeps completing an SFTP or SCP download negotiated with `blowfish-cbc` or `cast128-cbc` on
macOS. It does not reproduce the crash of upstream's macOS libssh2 build. This is an accepted known
difference from upstream curl.

## Why

- The crash is a defect of one platform's libssh2 build, not documented curl behaviour. No curl
  document asks for it.
- Exit 139 reports a process killed by a signal; it is not a curl exit code, so no script can
  reasonably depend on it.
- Copying it would make Curl fail where the other upstream builds that offer these ciphers download
  the file, which is a worse drop-in replacement.

## Consequences

- No product code changes. The offered algorithm lists stay those of ADR-0122 and ADR-1278.
- `SshProtocolHandlerTests.ExecuteAsync_ServerOffersOnlyALegacyCipherOrMac_TransfersOverIt` already
  pins the transfer over both ciphers with the OpenSSL algorithm lists.
- Surl's Phase 7 listing can mark the two
  `Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes` cases (`blowfish-cbc` and
  `cast128-cbc`) as an accepted known difference, citing this record.
- If a later upstream macOS build completes these downloads, the difference ends without any change
  to Curl.
