# ADR-0213 — SSH host keys are checked as curl's libssh2 build checks them, and a refusal is exit 60

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-566.

## Context

ADR-0122 gives `Curl.Protocol.Ssh.UnitLibrary` a `KnownHostsFile` and an
`SshHostKeyChecker` in `HostKeys`, and leaves to BL-566 how they behave: which of
`--hostpubsha256`, `--hostpubmd5` and the known-hosts file decides, what `-k` changes,
how hashed `|1|` names, `[host]:port` names and the `@revoked` and `@cert-authority`
markers are read, and what each refusal prints.

Measured 2026-09-29 with the Windows reference build (curl 8.21.0, libssh2 1.11.1 on
WinCNG, ADR-0122) against a throwaway loopback SSH server built from this library's
classes, on a port other than 22, with `-sS --knownhosts <file> -u u:p
sftp://127.0.0.1:<port>/x`. An accepted key goes on to authentication (the server then
closed, so curl ended with exit 79 `Error in the SSH layer`); a refused key ends there:

| Case | Exit | stderr |
| --- | ---: | --- |
| Host absent, empty file, `@revoked` or `@cert-authority` line alone, a matching line after an unparsable one | 60 | `curl: (60) SSL peer certificate or SSH remote key was not OK` and curl's four-line certificate help |
| Host present with another key (plain or hashed) | 60 | same |
| `[host]:port`, plain `host`, a comma list, hashed `[host]:port` or `host`, a mismatch before a match, `@revoked` beside a plain match | accepted | |
| Every known-hosts refusal above with `-k` | accepted | |
| `--hostpubmd5` right, either case | accepted | known hosts not consulted |
| `--hostpubmd5` wrong, with or without `-k` | 60 | `Denied establishing ssh session: mismatch MD5 fingerprint. Remote <hex> is not equal to <given>` |
| `--hostpubsha256` right, padded or not | accepted | |
| `--hostpubsha256` wrong, with or without `-k` | 60 | `Denied establishing ssh session: mismatch SHA256 fingerprint. Remote <base64> is not equal to <given>` |
| both given, SHA-256 right and MD5 wrong / SHA-256 wrong and MD5 right | 60 | the MD5 message / the SHA-256 message |
| an entry for the host of type `ssh-dss`, a certificate type or an unknown name | 79 | `Unknown host key type: 3932160`, before the key exchange |
| an SSH-1 (`RSA1`) entry for the host | 79 | `Found host key type RSA1 which is not supported` |

## Decision

`SshHostKeyChecker.Check` runs after the key exchange with the verified `K_S`, which
`SshKeyExchangeResult.HostKey` now carries. `--hostpubsha256` is compared first (base64
up to the first `=`, so padding is optional), then `--hostpubmd5` (hex, case ignored);
when either is given the known-hosts file is not consulted. Otherwise the known-hosts
file decides, and `-k` (a null `KnownHostsPath`, so no `KnownHostsFile`) accepts every
key. Every refusal is `CurlExitCode.PeerFailedVerification` (60) with the messages above.

`KnownHostsFile` reads text as libssh2 1.11.1 reads the file: `@revoked` and
`@cert-authority` are not understood and become entries for a host named after the
marker; the first unparsable line (no key, fewer than 20 characters after the names,
broken base64 in a hashed name) ends the reading; only `ssh-rsa`, the three
`ecdsa-sha2-nistp*` names and `ssh-ed25519` are recognized key types. The check searches
`[host]:port` then `host` (on port 22 `host` alone), considers only entries of the host
key's type, and matches on the base64 key text.

`SshHostKeyChecker.NarrowHostKeys` finds the entry curl narrows the host-key list by (the
first hashed entry, whatever it hashes; a plain name equal to the host; or
`[name]:port` with the port and a name the host starts with, curl comparing only that
length) and hands its type to `SshAlgorithmPreferences.NarrowHostKeysTo`; it is skipped
under `-k` and when `--hostpubmd5` is given, but not for `--hostpubsha256`, as curl's
source has it.

The file is opened through `IFileSystem` (`KnownHostsFile.LoadAsync`); a file that cannot
be opened reads as empty, so every host is not found.

Not measured, taken from libssh2 1.11.1's source: a hashed name whose hash is not 20
bytes never matches; a hashed name without its second `|` is skipped. A trailing CR is
removed from every line on every platform, as the Windows build's text-mode read does;
the OpenSSL build on Linux would keep it and fail such a key, a difference left
unreplicated because a CRLF known-hosts file there comes only from copying one across.

## Consequences

- The handler (BL-567 onward) loads the file when `KnownHostsPath` is set, narrows the
  preset before connecting, and calls `Check` after `ExchangeKeysAsync`.
- `-v` lines (`SSH: host check <n>`, `SSH: found host ...`) are BL-578's;
  `KnownHostsCheck` carries libssh2's numbers for them.
- Curl never writes the known-hosts file: curl's tool sets no key callback, so the
  add-to-file outcomes never happen.
