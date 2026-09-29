# ADR-0230 — SSH `publickey` asks, then signs, and a key that fails falls through to `password`

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-568.

## Context

ADR-0215 authenticates the user with `none`, `password` and `keyboard-interactive`; BL-568
adds `publickey` (RFC 4252 section 7) from `--key`, `--pubkey` and `--pass`, in the key
formats ADR-0122 lists. Which messages curl sends, which signature algorithm it picks,
where it looks for a key without `--key`, and what each failure prints had to be measured.

As in BL-567 there is no local OpenSSH server. The Windows reference build (curl 8.21.0,
libssh2 1.11.1 on WinCNG) was run through `Record-CurlExchange.ps1 -NoServer` with
`-sS -v -k -u tester:wrong sftp://127.0.0.1:<port>/x`, `HOME` set to an empty directory
unless the case says otherwise, against a throwaway loopback server built from this
library's classes (`diffie-hellman-group14-sha256`, `rsa-sha2-256`, `aes128-ctr`,
`hmac-sha2-256`, `ext-info-s`). It sent an `SSH_MSG_EXT_INFO` with `server-sig-algs`, listed
`publickey,password`, answered each `publickey` request as the case scripted, verified
every signature curl sent (all verified), and closed the channel curl opened after a
success, so those runs end `curl: (2) Failure initializing sftp session: Unable to startup
channel`. Keys were generated for the run with `ssh-keygen` and OpenSSL 3.5.

| Case | Client messages after `none` | Exit | `-v` reason, stderr |
| --- | --- | ---: | --- |
| RSA PKCS #1 PEM, `server-sig-algs` OpenSSH's list | `publickey` without signature (`rsa-sha2-512`), then signed | success | `SSH: authenticated via publickey` |
| same, list `rsa-sha2-256` / `rsa-sha2-256,rsa-sha2-512` / `ssh-rsa` | `rsa-sha2-256` / `rsa-sha2-512` / `ssh-rsa` | success | |
| same, no `EXT_INFO` | `ssh-rsa` | success | |
| same, list `ssh-ed25519` only | `password` | 67 | `No signing signature matched`; `curl: (67) Authentication failure` |
| RSA PEM encrypted (`AES-128-CBC`), `--pass` right | query, signed | success | |
| same, no `--pass` or a wrong one | `password` | 67 | `Reason unknown (-1)`; `Authentication failure` |
| RSA or ECDSA `openssh-key-v1`, ECDSA SEC 1 PEM (P-384), PKCS #8 RSA or ECDSA, encrypted PKCS #8, Ed25519, DSA PKCS #8 | `password` | 67 | `Reason unknown (-1)`; `Authentication failure` |
| `--key` missing | `password` | 67 | `Reason unknown (-1)`; `Authentication failure` |
| question answered `FAILURE` | `password` | 67 | `Username/PublicKey combination invalid`; `Authentication failure` |
| same, `keyboard-interactive` listed | `password`, `keyboard-interactive` | 67 | `curl: (67) Login denied` |
| signed request answered `FAILURE` | `password` | 67 | `Invalid signature for supplied public key, or bad username/public key combination` |
| question answered `SUCCESS` | nothing more | success | |
| `--pubkey` the key's `.pub` | query with the file's blob, signed | success | `SSH: trying public key file '<path>'` |
| `--pubkey` an ECDSA `.pub`, `--key` an RSA key; or `--key` missing | query with the ECDSA blob (`server-sig-algs` naming only RSA did not stop it), no signed request, `password` | 67 | `Callback returned error` |
| `--pubkey` missing | `password` | 67 | `Unable to open public key file` |
| list `password` only | `password` | 67 | no `publickey` line at all |
| no `--key`, `HOME/.ssh/id_rsa` exists | as the first row | success | `SSH: trying private key file '<HOME>/.ssh/id_rsa'` (forward slash on Windows) |
| no `--key`, only `HOME/.ssh/id_dsa` | as the first row | success | `'<HOME>/.ssh/id_dsa'` |
| no `--key`, `HOME` empty dir, `id_rsa` in the working directory | as the first row | success | `'id_rsa'` |
| no `--key`, `HOME` unset, `USERPROFILE/.ssh/id_rsa` exists, nothing in the working directory | `password` | 67 | `trying private key file ''` |
| `--key ""` | | 2 | `curl: option --key: blank argument where content is expected` |

After a failed `publickey` curl tried the SSH agent (`SSH: trying publickey authentication
via agent`, `SSH: failure connecting to agent`) before `password`; that is BL-902's. The
`-v` lines are BL-578's.

The Windows build reads only PKCS #1 RSA PEM, plain or with legacy PEM encryption: WinCNG
libssh2 reads no other format. The OpenSSL builds of Linux and macOS read every format in
ADR-0122's table through OpenSSL's decoders; their message flow is the same libssh2 code.

## Decision

- **Key files (`Keys` folder).** `SshPrivateKeyReader` reads every format ADR-0122 lists
  except what BL-681 adds, on every platform, as ADR-0122 decided: PKCS #1 RSA, OpenSSL
  DSA and SEC 1 EC PEM, each plain or with legacy PEM encryption (`AES-128/192/256-CBC`,
  `DES-EDE3-CBC`, `DES-CBC`, key from `EVP_BytesToKey` with MD5); PKCS #8, plain or
  encrypted (PBES2 with PBKDF2 over HMAC-SHA-1/-256/-384/-512 and AES-CBC, triple DES or
  DES; PBES1 with MD5 or SHA-1 and DES); and unencrypted `openssh-key-v1` RSA, DSA and
  ECDSA. The first PEM block decides. Anything else, a wrong or missing passphrase, and
  any malformation read as no key. Ed25519 and bcrypt-encrypted `openssh-key-v1` read as
  no key until BL-681, which adds them where `OpenSshPrivateKeyDecoder.Read` takes the
  private section and `Asn1PrivateKeyDecoder.ReadPkcs8` switches on the algorithm. The
  Windows build's narrower reading is not matched, as ADR-0122 decided: every format either
  backend reads is read, on every platform.
- **Signing.** `RsaSshPrivateKey` signs PKCS #1 v1.5 with the BCL; `EcdsaSshPrivateKey`
  signs P-256/384/521 with the BCL and writes r and s as `mpint`s; `DsaSshPrivateKey` signs
  with the hand-built `DsaSignature`, so DSA works on every platform. DES is the hand-built
  `Des`, since OpenSSL 3 platforms no longer offer single DES.
- **Files.** `SshUserKeySource` resolves `--key`, or else the first of
  `$HOME/.ssh/id_rsa`, `$HOME/.ssh/id_dsa`, `id_rsa` and `id_dsa` that opens, or else the
  empty path; `HOME` is read through an injected environment seam and joined with `/` on
  every platform, and an unset or empty `HOME` goes straight to the working directory (curl
  on Windows reads an empty variable as unset; on Unix curl would try `/.ssh/id_rsa`, which
  this does not). `--pubkey` empty counts as not given. `--pass` is encoded like the
  password (ADR-0022); without it the passphrase is empty.
- **Flow (`SshUserAuthentication`).** When the method list contains `publickey`, before
  `password`: read the public key (the `--pubkey` file, first line, `<type> <base64>`, as
  libssh2 parses it; else the private key's public half). None: the method fails and nothing
  is sent. For an `ssh-rsa` key, once an `EXT_INFO` has carried `server-sig-algs`, the
  algorithm is the first of `rsa-sha2-512`, `rsa-sha2-256`, `ssh-rsa` the server names,
  compared whole, and none of them fails the method unsent; before one, `ssh-rsa`. Other
  key types sign as their type, whatever `server-sig-algs` says. Send the request without a
  signature; `SUCCESS` authenticates, `PK_OK` leads to reading the private key and, when it
  is of the public key's type, sending the request with the flag set and a signature over
  the session identifier (as a string) and the request; `SUCCESS` to that authenticates.
  Anything else (`FAILURE`, a close, a disconnect, an unreadable or mismatched private key)
  fails `publickey` alone and curl goes on to `password`, so every key failure ends as
  ADR-0215's exit 67 `Authentication failure` or `Login denied`, never an exit of its own.
- **`EXT_INFO`.** Whenever the class reads a message it keeps the last `server-sig-algs` of
  an `SSH_MSG_EXT_INFO` (RFC 8308), which libssh2 reads only when at least five bytes long,
  stopping at a pair cut short. `SshTransport.SessionIdentifier` exposes the first
  exchange's hash for the signature.

## Consequences

- The handler (BL-576) builds one `SshUserKeySource` from the transfer's `SshOptions`, the
  file system, `Environment.GetEnvironmentVariable` and ADR-0022's encoding, and passes it
  to `SshUserAuthentication`; without one, `publickey` is skipped, as the tests of ADR-0215
  do.
- BL-902 slots the agent between a failed `publickey` and `password`.
- BL-681 adds Ed25519 signing and bcrypt decryption in the two places named above.
- BL-578 writes the `-v` lines measured here, including the reason libssh2 gave.
