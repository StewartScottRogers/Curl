---
id: BL-3963
title: Measure Curl's sftp/scp -w timings against a WSL sshd the Windows SSH preset can exchange keys with
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-28
completed:
---
# BL-3963 — Measure Curl's sftp/scp -w timings against a WSL sshd the Windows SSH preset can exchange keys with

## Goal

BL-3957's Notes record the Windows Curl build's -w timing output for the three SSH-08 repros (sftp and scp download of big.bin, sftp of a missing file), run against a local sshd.

## Context

BL-3957 ran a non-root OpenSSH 10.2 sshd in WSL on port 21957 (setup in its Notes). Real curl 8.18.0 (OpenSSL, WSL) measured fine. The Windows Curl build failed every attempt with exit 2 "Unable to exchange encryption keys", with an ed25519 host key and with an RSA one: by default the Windows SSH preset and OpenSSH 10 share no key exchange, cipher or MAC. Also, an sshd started as a daemon through `wsl -e` dies when that call returns ("startup pipe ... Connection reset"), so run it with `-D` in a process that stays alive. Set sshd_config's KexAlgorithms, Ciphers, MACs and HostKeyAlgorithms to ones the Windows preset offers (`Curl.Protocol.Ssh.UnitLibrary/Negotiation`), measure, and record the result.

## Acceptance criteria

- [ ] BL-3957's Notes record Curl's exit code and -w output on Windows for the three SSH-08 repros against a local sshd, and the sshd_config lines that let the key exchange succeed.

## Notes

## Log

- 2026-10-28: Created.
