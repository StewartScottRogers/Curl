# ADR-0189 — `--proto` scheme sets use one known-scheme list on every platform

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-522;
recorded in BL-746.

## Context

BL-522 taught `Curl.Cli.UnitLibrary` to read `--proto`, `--proto-redir` and `--proto-default`
(`CommandLineProtocolSet`: `KnownSchemes`, `Read`, `KnownScheme`). A value names schemes;
curl warns about a name its libcurl does not know (`Warning: unrecognized protocol '<name>'`)
and refuses an unknown `--proto-default`. Which names are "known" differs by build.

Measured with `Record-CurlExchange.ps1` against the Windows (Schannel) curl 8.21.0 on
2026-09-28 (the full table is in BL-522's Notes):

- `--proto ipfs,ws,wss,rtmp,scp` warns about `ipfs` and `rtmp`; `--proto smb` warns about `smb`.
  `ipfs` and `ipns` are on that build's `curl -V` `Protocols:` line, but the curl tool handles
  them itself and its libcurl does not know them.
- `--proto-default bogus`, `all`, `smb` and `ipfs` all fail with exit 1:
  `curl: option --proto-default: a specified protocol is unsupported by libcurl`.
- Names match case-insensitively: `HTTP`, `=HtTp`, `ALL` and `--proto-default HTTPS` are taken.

The Linux and macOS OpenSSL builds can be compiled with more protocols (such as `smb` and
`rtmp`), so `--proto smb` may be silent there.

## Decision

1. **One known-scheme list on every platform.** `KnownSchemes` is the Windows (Schannel) curl
   8.21.0 `Protocols:` line less `ipfs` and `ipns`: `dict file ftp ftps gopher gophers http
   https imap imaps ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws
   wss`. On Linux and macOS, `--proto smb` therefore warns where an OpenSSL build with SMB would
   not.
2. **Sets are lowercase and unordered.** `CommandLineOptions.AllowedProtocols` and
   `AllowedRedirectProtocols` are `IReadOnlySet<string>` of lowercase names, and
   `DefaultProtocol` is one lowercase name; `null` means the option was not given, which leaves
   curl's default.
3. **`--proto-default` refuses an unknown scheme with exit 1**, `all` included, with curl's
   `a specified protocol is unsupported by libcurl` message, as measured.

## Consequences

- Tests of these options pass unchanged on Windows, Linux and macOS, and the same command line
  prints the same warnings everywhere.
- Where an OpenSSL build knows a scheme that is not in the list, Curl warns about it (or refuses
  it as `--proto-default`) and that build would not. Adding a scheme to the list is a one-line
  change once Curl matches such a build.
- Order and case of the given names are not kept; enforcement (BL-523, BL-524) only asks
  whether a lowercase scheme is in the set.

## Alternatives considered

- **A list per platform** (the Schannel list on Windows, an OpenSSL build's list elsewhere):
  exact on each platform, but which protocols an OpenSSL build has depends on how it was
  compiled, so there is no one list to match, and tests would need a pinned answer per
  platform for every such scheme.
- **The full `Protocols:` line, `ipfs` and `ipns` included:** contradicts the measured warning
  and the `--proto-default ipfs` refusal.
- **Keeping the names as given (ordered list, original case):** nothing downstream needs order
  or case, and curl itself matches case-insensitively.
