# ADR-0835 — `smb` and `smbs` are disabled, as in curl 8.22.0's reference builds

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (tasks BL-3572 and BL-3887, CGR-043).

## Context

ADR-0523 took `smb` and `smbs` off `curl -V`'s `Protocols:` line on every platform, because neither
reference build of curl 8.22.0 (Schannel on Windows, the OpenSSL tarball build on Linux and macOS) has
SMB, but kept `smb://` and `smbs://` URLs working and both names in `--proto`'s known schemes. Gap report
issue CGR-043 measures what the reference does instead, and BL-3572 and BL-3887 measured it again on
2026-10-26 with the Windows curl 8.22.0 (Schannel):

- `curl -sS smb://127.0.0.1:20002/share/x` exits 1 with `curl: (1) Protocol "smb" is disabled`
  (`smbs` the same), writes nothing to standard output and connects nowhere; `-v` writes
  `* Protocol "smb" is disabled` before the `curl: (1)` line;
- `curl --proto =smb -sS smb://...` exits 2 with `curl: unrecognized protocol 'smb'`,
  `curl: option --proto: is badly used here` and the try-help line;
- `curl --proto-default smb -sS 127.0.0.1:20002/share/x` exits 1 with
  `curl: option --proto-default: a specified protocol is unsupported by libcurl` and the try-help line.

Curl connected and failed with exit 7 for all three.

## Decision

1. `EngineComposition.CreateProtocolHandlers` serves `smb` and `smbs` with a `DisabledProtocolHandler`,
   which fails every transfer with exit 1 and `Protocol "<scheme>" is disabled` before connecting, also
   as the `-v` info line. The curl tool, `Curl.Api` and `Libcurl` all compose their handlers there, so
   all three refuse alike, as libcurl built without SMB does (BL-3887).
2. `CommandLineProtocolSet` reads `--proto` and `--proto-redir` against `KnownSchemes` less `smb` and
   `smbs`, so both refuse them as unrecognized and `all` leaves them out (BL-3888); `--proto-default`
   is to refuse them as unsupported (BL-3889).
3. `Curl.Original.Protocol.Smb.UnitLibrary` stays, with its tests, as the SMBv1 client a build with SMB would
   use; the conformance harness's smbserver stand-in still serves it, driven by that handler.
   Upstream's smb cases (1451, 1850) leave the ratchet: the harness no longer reports the `smb`
   feature, so they are skipped, as upstream's runtests skips them for the reference build.

## Why

- Matching the platform's curl is the standing rule, and both reference builds refuse SMB the same
  way, so the refusal is the same on every platform.
- "Is disabled", not "not supported", is what the reference prints: libcurl knows the scheme's name
  and refuses it, which a handler registered for the scheme gives without a second lookup table in
  the dispatcher.

## Consequences

- A script that ran `curl smb://...` against Curl now gets exit 1, as it would with the reference curl.
- Supersedes the parts of ADR-0189, ADR-0200 and ADR-0523 that kept SMB working and known to `--proto`.
