---
id: BL-566
title: Verify the SSH host key against known_hosts, --hostpubmd5 and --hostpubsha256
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0213-ssh-host-keys-are-checked-as-curl-s-libssh2-build-checks-them-and-a-refusal-is-exit-60.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-566 — Verify the SSH host key against known_hosts, --hostpubmd5 and --hostpubsha256

## Goal

The server's host key is accepted or refused as curl 8.21.0 does: matched against `--hostpubsha256` (base64 SHA-256) or `--hostpubmd5` (hex MD5) when given, else against the known-hosts file (`--knownhosts`, default `~/.ssh/known_hosts`) including hashed `|1|` entries, `[host]:port` entries and `@revoked`/`@cert-authority` markers as curl treats them, with `-k` changing the outcome as curl's does, and every refusal mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, rows 31 and 35. Builds on BL-564 (the host key and its verified signature).
- **BCL only.** `MD5`, `SHA256`, `HMACSHA1` (hashed known_hosts entries). Known-hosts entries of every key type BL-560's ADR offers are matched, `ssh-ed25519` included (its signature check is BL-678), and `@cert-authority` covers `ssh-ed25519-cert-v01@openssh.com` host certificates as curl's libssh2 build treats them. What the BCL lacks is hand-built in `Curl.Cryptography.UnitLibrary` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- The known-hosts reader takes text, not a path (tests need no disk); the file is opened through the seam BL-560's ADR names.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: host absent from known_hosts, present with a matching key, present with a different key, a hashed entry, `--hostpubmd5` right and wrong, `--hostpubsha256` right and wrong, and each wrong case with `-k`.

## Acceptance criteria

- [x] Measured first as above; stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer and known-hosts text.
- [x] New tests are platform-neutral (no home-directory path assumed; the default path is resolved through the environment seam).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Design (ADR-0213).** `HostKeys` gains `KnownHostsFile` (parses text; `LoadAsync`
  opens it through `IFileSystem`, an unopenable file reads as empty), `KnownHostsEntry`,
  `KnownHostKeyType`, `KnownHostKeyTypeNames`, `KnownHostsCheck` (libssh2's 0/1/2),
  `Libssh2Base64` (libssh2's lenient decoder for hashed names) and `SshHostKeyChecker`
  (`Check` after the exchange, `NarrowHostKeys` before it, feeding
  `SshAlgorithmPreferences.NarrowHostKeysTo`). `SshKeyExchangeResult` now carries
  `HostKey`. The handler that calls them does not exist yet (BL-567 onward).
- **Measured 2026-09-29, Windows reference build** (curl 8.21.0, libssh2 1.11.1 WinCNG),
  `curl -sS --knownhosts <file> -u u:p [options] sftp://127.0.0.1:<port>/x` through
  `Record-CurlExchange.ps1 -NoServer` against a throwaway MSTest loopback SSH server built
  from this library's classes (group14-sha256, rsa-sha2-256, aes128-ctr, hmac-sha2-256;
  it answered `SERVICE_ACCEPT`, then closed; deleted before commit). No local sshd
  exists (Git for Windows ships none; WSL and Docker down). Accepted keys went on to
  authentication and ended `curl: (79) Error in the SSH layer` when the server closed.
  - host absent / empty file / `@revoked` alone / `@cert-authority` alone / match after
    an unparsable line (`ssh-rsa AAAA`) / another key (plain or hashed) / another port:
    exit 60, `curl: (60) SSL peer certificate or SSH remote key was not OK` plus curl's
    four-line sslcerts help (the console adds that trailer).
  - `[host]:port`, plain `host` on port != 22, comma list with comment, hashed
    `[host]:port`, hashed `host`, mismatch then match, `@revoked` plus plain match:
    accepted. Each refusal with `-k`: accepted.
  - `--hostpubmd5` right (lower or upper case): accepted, known hosts skipped. Wrong,
    with or without `-k`: exit 60, `curl: (60) Denied establishing ssh session: mismatch
    MD5 fingerprint. Remote 13ea374d42bde4d02f79dfd6fd4a80c0 is not equal to
    00112233445566778899aabbccddeeff`. A 30-digit value: the CLI refuses it (exit 2,
    `option --hostpubmd5: is badly used here`, BL-562's).
  - `--hostpubsha256` right, padded or not: accepted. Wrong, with or without `-k`: exit
    60, `Denied establishing ssh session: mismatch SHA256 fingerprint. Remote
    TotB29FECq0vDjDG5d4lpgnAOuaDB1mxpJXafuxgob4= is not equal to
    AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=`. Both given: SHA-256 is checked first.
  - Entry for the host of type `ssh-dss`, `ssh-rsa-cert-v01@openssh.com` or `foo-bar`:
    exit 79 `curl: (79) Unknown host key type: 3932160`, before `KEXINIT`. `RSA1` entry:
    exit 79 `Found host key type RSA1 which is not supported`.
  - `-v` lines seen (BL-578's): `SSH: found host '127.0.0.1' in '<file>'`, `SSH: did not
    find host ...`, `SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type`,
    `SSH: host check 0|1|2, key: <base64>|<none>`, `SSH: knownhost entry matches host
    key`, `SSH: knownhost check failed`, `SSH: MD5 public key '<given>'`, `SSH: MD5
    fingerprint '<hex>'`, `SSH: SHA256 public key ...`, `SSH: SHA256 checksum match`.
- **Tests** pin each case with a fixed P-256 host key (its fingerprints written out as
  literals), so the messages carry that key's fingerprints, not the measured RSA key's.
- **Default path.** The reader takes text and the path comes from
  `SshOptions.KnownHostsPath`, which the console resolves (ADR-0122, BL-576); no test here
  assumes a home directory.
- **Not measured, from libssh2 1.11.1's source** (ADR-0213): hashed hash not 20 bytes
  never matches; hashed name without its second `|` skipped; narrowing skipped for
  `--hostpubmd5` only; `[name]:port` narrowing compares only the bracketed length. A
  trailing CR is dropped on every platform (Windows text-mode behaviour).
- **Touches.** Added the ADR file and `Documentation/Planning/Decisions/README.md`: the
  decision needed an ADR, and no task in Doing names either.
- `dotnet format --verify-no-changes` flags LF line endings in two files this task did
  not touch (`Libssh2ErrorCode.cs`, `SshPacketAuthenticationException.cs`); left alone.
- **Re-integration 2026-09-29.** The first run's work was cherry-picked unchanged from
  `factory/BL-566-lane-1-20260929-061257` onto the current base. Build clean, all 33
  fast test projects green (SSH 348 tests), SSH library still 100/100 with 0 failing
  members; the earlier integration failure did not reproduce, so it came from another
  lane's work, since fixed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-566-lane-1-20260929-061257; start with git cherry-pick --no-commit factory/BL-566-lane-1-20260929-061257 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH host keys are accepted or refused as curl 8.21.0 does: --hostpubsha256, --hostpubmd5, known_hosts (hashed, [host]:port, markers) and -k, refusals exit 60; SSH library at 100/100
