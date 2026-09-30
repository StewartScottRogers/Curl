# ADR-0268 — SSH group exchange asks for the platform build's sizes: up to 4096 bits on Windows, 8192 on OpenSSL

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-888.
Amends ADR-0206's group-size decision, which pinned (2048, 4096, 4096) on every preset
because only the Windows build could be measured; the rest of ADR-0206 stands.

## Context

`diffie-hellman-group-exchange-sha256` and `-sha1` open with the client's
`SSH_MSG_KEX_DH_GEX_REQUEST` (message 34): the minimum, preferred and maximum prime
sizes in bits (RFC 4419). They are also hashed into H, so a drop-in replacement must send
exactly what the platform's curl sends, and refuse a group outside the range it asked for.

Measured 2026-09-29 against the OpenSSL reference build (`curlimages/curl:8.21.0`:
`curl 8.21.0 (x86_64-pc-linux-musl) ... OpenSSL/3.5.7 ... libssh2/1.11.1`), with

```
Record-CurlExchange.ps1 -Port 24888 -ListenAddress 0.0.0.0 -Response <bytes> -HoldOpenMilliseconds 3000 `
  -Curl docker.exe -CurlArgs 'run','--rm','curlimages/curl:8.21.0','-s','-S','-k','-u','u:p','-m','10','sftp://host.docker.internal:24888/x'
```

where the response is `SSH-2.0-OpenSSH_9.7\r\n` and a `KEXINIT` offering only
`diffie-hellman-group-exchange-sha256` (then, in a second run, only `-sha1`),
`rsa-sha2-256`, `aes128-ctr`, `hmac-sha2-256` and `none`. After its own `KEXINIT` the
client sent, for both methods, the payload `22 00000800 00001000 00002000`: minimum 2048,
preferred 4096, maximum 8192. It then exited 2 with
`curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys`
when the server sent nothing more. The Windows build (WinCNG) sends
`22 00000800 00001000 00001000` (ADR-0206).

Both builds run libssh2 1.11.1, so the difference is its cryptography backend's (the
WinCNG build's largest usable prime is 4096 bits); the source was not read to confirm it.

## Decision

- **The sizes belong to the preset.** `SshAlgorithmPreferences.GroupExchangeSizes`
  (an `SshGroupExchangeSizes(MinimumBits, PreferredBits, MaximumBits)`) is
  `SshGroupExchangeSizes.WindowsReference`, (2048, 4096, 4096), on
  `SshAlgorithmPreferences.WindowsReference`, and `SshGroupExchangeSizes.OpenSslReference`,
  (2048, 4096, 8192), on every other preset: `OpenSslReference`, `Full`, and any preset
  built without naming them, since those are libssh2's own values.
- **`GroupExchangeSshKeyExchange` sends, hashes and enforces the preset's sizes**: a prime
  under the minimum or over the maximum is refused with ADR-0206's `-8` failure, exit 2.
  So an 8192-bit group is used on Linux and macOS and refused on Windows, as each
  platform's curl does.
- `SshTransport` hands the preset's sizes to `SshKeyExchangeMethods.Create` for each
  exchange, re-exchanges included; `--compressed-ssh` and known-hosts narrowing keep them.

## Consequences

- `SshTransportTests` pins the request each preset sends for both hashes, and runs a
  full exchange on each preset with a prime at its maximum (group 16 on Windows, group
  18 on OpenSSL), and refuses one past it.
- A preset for another SSH backend names its own sizes once it is
  measured.
