# ADR-1167 — --ssh-jump reaches servers through a direct-tcpip channel

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4145.

Builds on ADR-0999 (extensions), ADR-1165 and ADR-1166 (the shared `Curl.Ssh.UnitLibrary`).

## Context

BL-4145 asks for an extension that reaches servers through an SSH jump host, as OpenSSH's
`ProxyJump` does. upstream curl has nothing like it, so the extension is tested against its
specification: RFC 4254 (January 2006), section 7.2 `direct-tcpip` channels, over the
transport of RFC 4253 and the authentication of RFC 4252.

## Decision

- **Option name.** `--ssh-jump <user[:password]@host[:port]>`, port 22 when left out. The
  name follows OpenSSH's `ssh -J`; the value is OpenSSH's `[user@]host[:port]` with the user
  required, since curl has no SSH configuration file to supply one, and an optional password
  as curl's own `-u user:password` takes it. A value that is not that shape is refused as a bad
  option argument.
- **Where it acts.** `Curl.Extension.SshJump.UnitLibrary` registers a connection hook (the
  BL-4095 hook, in `ExtensionConnectionHookCatalogue.cs`), not a protocol handler, so it has no
  line in `ExtensionCatalogue.cs`. The hook wraps curl's own connector: curl's connector reaches
  the jump host, an SSH session to it opens one `direct-tcpip` channel to the URL's host and port,
  and the channel is the transfer's connection. SFTP and SCP run their own SSH session over the
  channel (end-to-end, as `ProxyJump` does); TLS schemes run curl's TLS handshake over it with
  curl's ALPN list; every other scheme is tunnelled as plain TCP.
- **The session to the jump host** is the same client curl's SFTP and SCP use
  (`Curl.Ssh.UnitLibrary`): the jump host's key is checked against the user's known-hosts file
  before anything is authenticated, then the password, the ssh-agent (Pageant on Windows) and
  the user's key files are tried, as curl's SSH does.
- **Channel open fields.** The originator address and port are `127.0.0.1` and `65535`, as
  OpenSSH's `ssh -W` sends for a channel with no local socket behind it.
- **Exit codes.** A refused channel open fails with exit 7 (couldn't connect), naming RFC 4254's
  reason code; a failed handshake, host-key check or authentication keeps curl's SSH exit code
  for it; the jump host closing or breaking the protocol is exit 7.
- **Without the option** the hook is not activated and every transfer is unchanged; with
  `CURL_EXTENSIONS=0` the option is unknown and refused with exit 2, as for every extension.

## Left out

- **UDP (HTTP/3, QUIC).** A `direct-tcpip` channel carries TCP only, and sending QUIC past the
  jump host would reach the server from this machine, which the option exists to prevent; a
  datagram socket under `--ssh-jump` fails with exit 7.
- **Chains of jump hosts** (`ssh -J a,b`). One hop covers the stated goal; a chain is a later
  extension of the value's syntax, not a different design.
- **An SSH configuration file** (`~/.ssh/config` host aliases). curl reads none, and the
  extension reads only what curl's SSH already reads.
