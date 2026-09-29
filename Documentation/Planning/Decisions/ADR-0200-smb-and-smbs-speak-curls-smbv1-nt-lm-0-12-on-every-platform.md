# ADR-0200 — `smb` and `smbs` speak curl's SMBv1 "NT LM 0.12" on every platform, authenticated with NTLMv1 responses

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-594.
Builds on ADR-0021 (`-V` lists only what Curl implements), ADR-0120 (protocol libraries
may reference the hand-built libraries) and ADR-0156 (NTLM hand-builds DES and answers as
curl 8.21.0 does).

## Context

The standing rule (Stewart, 2026-09-28) is that a feature any official curl build supports
is supported on every platform, so this ADR decides how `smb://` and `smbs://` are built,
not whether. Measured on 2026-09-29:

- **Windows reference build** (curl 8.21.0, Schannel, mingw64): `Protocols:` has no `smb`
  or `smbs`. `curl -sS smb://127.0.0.1/share/x.txt` prints
  `curl: (1) Protocol "smb" is disabled` and exits 1; with `-v` the same text also
  appears as `* Protocol "smbs" is disabled`. Recorded for the record only.
- **Linux OpenSSL build** (Ubuntu's curl 8.18.0, WSL): `Protocols:` lists `smb smbs`
  between `sftp` and `smtp`. No Samba server was available, so the request side was
  recorded with a loopback listener (`nc -l 127.0.0.1 4450`, since
  `Record-CurlExchange.ps1` runs only the Windows curl, which has no SMB). Running
  `curl -v -u user:pw smb://127.0.0.1:4450/share/x.txt`, curl's first and only bytes
  before the server answers are the NetBIOS session header and an SMBv1 NEGOTIATE:

  ```
  0000002f ff534d42 72 00000000 18 4100 ...
  ... 000c00 02 4e54204c4d20302e313200     (one dialect: "NT LM 0.12")
  ```

  That is command `0x72`, flags `0x18` (case-insensitive, canonical paths), flags2
  `0x0041` (long names known and used; no Unicode, no extended security, no signing), a
  byte count of 12 and a single dialect string. With the silent server the run ends
  `* Operation timed out after 1999 milliseconds with 0 bytes received` and exit 28; a
  refused port is exit 7 with the usual `Failed to connect` lines.
- **curl's source** (`lib/smb.c` at tag `curl-8_21_0`) confirms curl's SMB is SMBv1 only
  and names the rest of the exchange: SESSION_SETUP_ANDX (`0x73`) carrying 24-byte LM and
  NT responses to the 8-byte challenge the NEGOTIATE response holds (not NTLMSSP),
  TREE_CONNECT_ANDX (`0x75`) to `\\host\share`, NT_CREATE_ANDX (`0xa2`), READ_ANDX
  (`0x2e`) or WRITE_ANDX (`0x2f`), CLOSE (`0x04`) and TREE_DISCONNECT (`0x71`). The
  default port for both schemes is 445; `smbs` is the same exchange inside TLS from the
  first byte.

## Decision

- **Offered everywhere.** `smb` and `smbs` are handled on Windows, Linux and macOS, and
  once BL-598 registers the handler, `-V` lists them on every platform (ADR-0021).
- **Dialect and messages.** Curl speaks exactly what curl 8.21.0 speaks: SMBv1 over direct
  TCP (NetBIOS session header, no NetBIOS name service), one dialect `NT LM 0.12`, flags
  `0x18` and flags2 `0x0041`, and the seven messages above in curl's order. No SMB2/3 and
  no signing, because curl offers neither; a server that refuses SMBv1 fails as it does
  with curl.
- **NTLM route.** `Curl.Protocol.Smb.UnitLibrary` references `Curl.Ntlm.UnitLibrary`
  (ADR-0120) and computes the LM and NT responses with
  `NtlmResponseComputation.ComputeV1` over the NEGOTIATE response's challenge. The NTLMSSP
  message types are not used: SMBv1 without extended security carries the raw responses.
  The user comes from `-u` or the URL; a `DOMAIN\user` or `DOMAIN/user` name splits into
  domain and user as curl does, and with no domain the URL's host name is sent as the
  domain, as `smb_connect` in `smb.c` does.
- **Output text.** Every platform matches a curl build that has SMB, i.e. the OpenSSL
  build's text, because no Windows reference output exists to match. SMB's own `-v` lines
  and errors carry nothing platform-specific; connection and TLS lines stay each
  platform's as they already are.
- **Tests stay off the network.** The handler takes `IConnection` (the library's
  `CLAUDE.md`) and each message is built and parsed by its own type, so the
  `.UnitTests` project drives it from recorded byte streams: curl's NEGOTIATE above is
  pinned byte for byte, and the server's side is hand-assembled from `smb.c`'s structures
  and MS-CIFS. A Samba capture replaces a hand-assembled response when one is recorded.

## Consequences

- **BL-595** negotiates the session and authenticates it with NTLMv1 responses from
  `Curl.Ntlm.UnitLibrary`, pinning the measured NEGOTIATE bytes.
- **BL-596** connects to the share (TREE_CONNECT_ANDX, NT_CREATE_ANDX) and downloads with
  READ_ANDX, then CLOSE and TREE_DISCONNECT.
- **BL-597** uploads with `-T` through WRITE_ANDX.
- **BL-598** registers the handler for `smb` and `smbs` in `Curl.Console`, lists both in
  `-V` on every platform and prints curl's `-v` lines.
- `Curl.Protocol.Smb.UnitLibrary/CLAUDE.md` still says it references only the
  abstractions; BL-595 adds the `Curl.Ntlm.UnitLibrary` reference and updates that line.
- SMBv1 is weak and off by default in modern Windows and Samba; Curl inherits that, as
  curl does. Matching curl, not bettering it, is what a drop-in replacement owes.
- The server-side fixtures are hand-assembled until a Samba capture is recorded, so a
  mistake in reading MS-CIFS could be pinned; the request side, which is what curl
  compatibility turns on, is measured.

## Alternatives considered

- **SMB2/3 as well.** Real servers prefer it, but curl 8.21.0 does not speak it; offering
  it would change the bytes on the wire and the servers a script can reach.
- **NTLMSSP with extended security.** More secure, but not what curl sends; its flags2
  lacks the extended-security bit.
- **Leave `smb` out on Windows to match the Schannel build.** Ruled out by the standing
  rule: an official build supports it, so every platform does.
