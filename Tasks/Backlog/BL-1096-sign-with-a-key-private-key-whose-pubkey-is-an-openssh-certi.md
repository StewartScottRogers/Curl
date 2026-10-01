---
id: BL-1096
title: Sign with a --key private key whose --pubkey is an OpenSSH certificate, as libssh2 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-1036]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1096 — Sign with a --key private key whose --pubkey is an OpenSSH certificate, as libssh2 does

## Goal

`curl --key id_rsa --pubkey id_rsa-cert.pub sftp://...` (and the ECDSA and Ed25519 certificate forms) authenticates with the certificate as curl's libssh2 1.11.1 does, instead of failing the method because the private key's type is not the certificate's (`SshInfoLines.SignCallbackFailed`).

## Context

`SshUserAuthentication.SendSignedPublicKeyAsync` fails the method when the private key's `KeyType` differs from the public key's, and a certificate's type (`ssh-rsa-cert-v01@openssh.com`, ...) never equals its private key's. libssh2's `libssh2_userauth_publickey_fromfile_ex` signs with the private key file under the chosen method and sends the certificate blob; the signature names `plain_method`'s form. BL-1036 (ADR-0310) already chooses the upgraded certificate method on OpenSSL. Found in BL-1036. Measure first with the reference curl (Windows) and the WSL Ubuntu curl (libssh2 1.11.1, OpenSSL) against a loopback server that accepts the certificate, as ADR-0310 did, including a certificate whose key is not the private key's.

## Acceptance criteria

- [ ] Measured first, in Notes: the request, signature method and `-v` lines for an RSA and an Ed25519 certificate on both builds, and for a certificate whose key does not match `--key`.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
