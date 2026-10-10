---
id: BL-1951
title: Serve libssh2 WinCNG's key exchange, RSA host key and RSA client key in the SSH stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1951 — Serve libssh2 WinCNG's key exchange, RSA host key and RSA client key in the SSH stand-in

## Goal

On Windows, where Curl matches curl's libssh2 1.11.1 WinCNG build, Curl's SSH client completes the key exchange with the conformance SSH stand-in and can authenticate to it with the stand-in's client key.

## Context

Found by BL-1916 (2026-10-10). Curl.Conformance.SshServer.UnitLibrary's `SshServerTransport.ExchangeKeysAsync` offers only `curve25519-sha256` (and the libssh.org alias) with an `ssh-ed25519` host key. Curl's SSH client on Windows reports `SSH: libssh2 cryptography backend: WinCNG` and offers no Curve25519 or Ed25519 (`SshAlgorithmPreferences.WinCngBackend`; `SshUserAuthentication` notes WinCNG reads no Ed25519 or ECDSA private key), so every SSH case ends `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`, and the stand-in's session faults with "The SSH client offers no algorithm list the server shares". Measured with a probe: `-u curltest:curltest-password --insecure sftp://127.0.0.1:9003/x` through `UpstreamConformanceTests.RunCurlAsync` against `new SshServerConnector(new SystemSshRandomSource())` on Windows.

Add to the stand-in, BCL only, reusing the client's own key-exchange code from Curl.Protocol.Ssh.UnitLibrary (KeyExchange folder) through InternalsVisibleTo: a key exchange WinCNG's libssh2 offers (`ecdh-sha2-nistp256` via `ECDiffieHellman`, or `diffie-hellman-group14-sha256`), an `rsa-sha2-256` / `ssh-rsa` host key (fixed, so `--hostpubmd5` / `--hostpubsha256` fingerprints stay stable; name them in the library's CLAUDE.md), and an RSA client key as the account's key on Windows (upstream's sshserver.pl generates RSA keys). BL-1916's uncommitted work (shelved by the shift) adds `SshServerClientAccount`, `SshServerUserAuthentication` and `SshServerSessionChannel` to the same library: keep the change to `SshServerTransport` and the host key so the two merge.

## Acceptance criteria

- [ ] A unit test in Curl.Conformance.SshServer.UnitTests runs the key exchange with the client configured as WinCNG's libssh2 (`SshAlgorithmPreferences` for the WinCNG backend) and gets matching session identifiers.
- [ ] The Ed25519 path stays: the existing `SshServerConnectorTests` still pass.
- [ ] The library's CLAUDE.md names every host key's `--hostpubmd5` and `--hostpubsha256` fingerprint.
- [ ] Curl.Conformance.SshServer.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests are platform-neutral with no TestCategory=Integration.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
