---
id: BL-5106
title: Skip host-key type narrowing when --hostpubsha256 is given
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs, Curl.Ssh.UnitTests/HostKeys/HostPublicKeySha256NarrowingTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5106 — Skip host-key type narrowing when --hostpubsha256 is given

## Goal

With `--hostpubsha256`, SFTP and SCP do not narrow the offered host-key types from known_hosts, so a valid ed25519 fingerprint is accepted even when known_hosts lists an ssh-rsa key first.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-059 (Low). Confirmed against OpenSSL curl 8.18 on Linux (twice); Windows curl 8.22 could not negotiate with this sshd, so 8.22 was not checked. Area: SFTP/SCP, Linux build.

**Repro (Linux).**
1. Run OpenSSH sshd on 127.0.0.1:25022 with both an RSA and an ed25519 host key.
2. `ssh-keyscan -p 25022 127.0.0.1 > $HOME/.ssh/known_hosts` (the ssh-rsa line comes first).
3. `ED=$(ssh-keygen -lf hostkey_ed.pub -E sha256 | cut -d' ' -f2 | sed s/SHA256://)`
4. `curl -sS --hostpubsha256 $ED sftp://$USER@127.0.0.1:25022/etc/hostname`

**Expected (upstream).** Exit 0, file downloaded; with `-v`, no host-key-type line when `--hostpubsha256` or `--hostpubmd5` is given.

**Actual.** `* SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type`, then `curl: (60) Denied establishing ssh session: mismatch SHA256 fingerprint. Remote ...(RSA)= is not equal to ...(ed25519)`. `--hostpubmd5` with the ed25519 MD5 works.

**Where to start.** `Curl.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs`, `NarrowHostKeys` skips narrowing only when `options.HostPublicKeyMd5 is not null`; also skip it when a SHA256 fingerprint is set.

## Acceptance criteria

- [ ] New tests in `Curl.Ssh.UnitTests/HostKeys/HostPublicKeySha256NarrowingTests.cs` prove that with a SHA256 fingerprint option set and known_hosts content whose first entry is ssh-rsa, the offered host-key algorithm list is not narrowed (same as the MD5 case), and no host-key-type verbose line is emitted.
- [ ] A regression test proves narrowing from known_hosts still happens when neither fingerprint option is set.
- [ ] A test proves a wrong SHA256 fingerprint is still refused with exit 60.
- [ ] Tests are platform-neutral and use in-memory known_hosts content.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check on Linux: step 4 exits 0 and prints the file.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 11ac5260 98d07668 3183c2de b3b4034e a0b49a29.
