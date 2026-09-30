---
id: BL-888
title: Measure the OpenSSL curl build's SSH group-exchange request sizes and split them by preset if they differ
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-564]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-888 — Measure the OpenSSL curl build's SSH group-exchange request sizes and split them by preset if they differ

## Goal

`diffie-hellman-group-exchange-*` asks for, and accepts, the group sizes the platform's curl asks for: the Windows build's measured (2048, 4096, 4096), and the OpenSSL build's as measured.

## Context

- ADR-0206 (BL-564) pinned `GroupExchangeSshKeyExchange.MinimumBits`/`PreferredBits`/`MaximumBits` at (2048, 4096, 4096) on every preset, measured from the Windows reference build (`curl 8.21.0 ... libssh2/1.11.1`, WinCNG). The OpenSSL reference build (`curlimages/curl:8.21.0`) could not be measured that day because the Docker engine was down.
- Measure: run the container's curl with `-s -S -k -u u:p -m 10 sftp://host.docker.internal:<port>/x` against a listener that sends `SSH-2.0-OpenSSH_9.7\r\n` and a `KEXINIT` offering only `diffie-hellman-group-exchange-sha256` (the builder BL-564 used is described in its Notes), and decode the three `uint32`s of the client's `SSH_MSG_KEX_DH_GEX_REQUEST` (message 34) that follows its `KEXINIT`.
- If they differ, move the sizes onto `SshAlgorithmPreferences` (the Windows preset keeps (2048, 4096, 4096)) and amend ADR-0206 with a new ADR.

## Acceptance criteria

- [x] The OpenSSL build's (minimum, preferred, maximum) is recorded in Notes with the command that measured it.
- [x] A `Curl.Protocol.Ssh.UnitTests` test pins the request each preset sends, and a group-exchange exchange passes for a prime at the preset's maximum.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured 2026-09-29

Reference: `curlimages/curl:8.21.0` (`curl 8.21.0 (x86_64-pc-linux-musl) ... OpenSSL/3.5.7 ... libssh2/1.11.1`),
Docker Desktop started for the run. Command (response built in PowerShell: `SSH-2.0-OpenSSH_9.7\r\n`
and a `KEXINIT` offering only the method, `rsa-sha2-256`, `aes128-ctr`, `hmac-sha2-256`, `none`):

```
Record-CurlExchange.ps1 -Port 24888 -ListenAddress 0.0.0.0 -Response <bytes> -HoldOpenMilliseconds 3000 `
  -Curl docker.exe -CurlArgs 'run','--rm','curlimages/curl:8.21.0','-s','-S','-k','-u','u:p','-m','10','sftp://host.docker.internal:24888/x'
```

| Method | `SSH_MSG_KEX_DH_GEX_REQUEST` payload | (min, n, max) |
| --- | --- | --- |
| `diffie-hellman-group-exchange-sha256` | `22 00000800 00001000 00002000` | (2048, 4096, 8192) |
| `diffie-hellman-group-exchange-sha1` | `22 00000800 00001000 00002000` | (2048, 4096, 8192) |

Exit 2, `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys` (no group sent).
The OpenSSL build differs from Windows' (2048, 4096, 4096), so the sizes were split by preset.

### Decisions (ADR-0268, amends ADR-0206)

- `SshGroupExchangeSizes` (KeyExchange) is carried on `SshAlgorithmPreferences.GroupExchangeSizes`
  (internal init property): Windows (2048, 4096, 4096); OpenSSL, `Full` and any unnamed preset
  (2048, 4096, 8192), libssh2's own values. `SshTransport` passes it to `SshKeyExchangeMethods.Create`.
- `Full` takes the OpenSSL sizes: it is the widest set and nothing in the console selects it.

### Scope

- `touches` gained `Documentation/Planning/Decisions` for ADR-0268, ADR-0206's amendment and the
  index rows; the only other task in Doing (BL-717) does not name it.
- Tests: `ExchangeKeysAsync_GroupExchangeOnEachPreset_AsksForThePresetsSizesAndAcceptsAPrimeAtItsMaximum`
  (both presets, both hashes; group 16 on Windows, group 18 on OpenSSL) and
  `ExchangeKeysAsync_UnusableGroupExchangeGroup_FailsWithMinus8` now per preset (8192 bits refused
  on Windows, 8193 on OpenSSL). Ssh tests 1304 pass; `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Ssh.UnitLibrary`: 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH group exchange asks for and accepts each platform build's measured sizes: (2048, 4096, 4096) on Windows, (2048, 4096, 8192) on OpenSSL
