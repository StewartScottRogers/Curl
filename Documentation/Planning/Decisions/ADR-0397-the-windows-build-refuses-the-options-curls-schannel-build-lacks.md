# ADR-0397 — The Windows build refuses the options curl's Schannel build lacks

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1278.
Amends ADR-0141, ADR-0144, ADR-0151 and ADR-0328 for Windows only.

## Context

ADR-0141 (HTTP/2), ADR-0144 (HTTP/3), ADR-0151 (the ten TLS options, `--ssl-sessions` among them)
and ADR-0328 (`--tlsuser` runs TLS-SRP) accepted their options on every platform. curl 8.21.0's
Windows Schannel build - the Git for Windows `mingw64` curl and the inbox
`C:\Windows\System32\curl.exe` - was built without HTTP/2, HTTP/3, TLS-SRP and SSL session export,
so it refuses those options. The audit office's conformance auditor reported the difference as
AF-0022 (Medium): 25 of 300 generated command lines exited 2 under curl and ran a transfer under
Curl. Stewart accepted the finding, which makes the platform-build rule ("match the Schannel build on
Windows") win over the earlier decisions on Windows.

Measured with curl 8.21.0 (x86_64-w64-mingw32) Schannel on 2026-10-02 (BL-1278 Notes):

- `--http2`, `--http2-prior-knowledge`, `--http3`, `--http3-only` (also `--http2=x`), and
  `--tlsuser`, `--tlspassword`, `--tlsauthtype`, `--proxy-tlsuser`, `--proxy-tlspassword`,
  `--proxy-tlsauthtype` and `--ssl-sessions` with any value, empty included, print
  `curl: option <as typed>: the installed libcurl version does not support this` and the try-help
  line, exit 2, even under `-s`, and send nothing.
- A value option given as the last argument is still refused as `requires parameter` first.
- The `--no-` forms stay refused as not reversible.
- In a `-K` file the line is `<file>:<n> config file option '<name>' the installed libcurl version
  does not support this`, then `curl: option -K: ...`.

## Decision

When the parser reads as the Windows build (`CommandLineParser`'s `isWindows`, this process's platform
unless a caller says otherwise), the eleven rows built with `CommandLineOption.RefusedBySchannelBuild`
refuse with `CommandLineRefusal.InstalledLibcurlDoesNotSupport` before their value is checked
(`CommandLineOptions.ActsAsWindowsSchannelBuild`). Off Windows nothing changes: they are accepted as
curl's OpenSSL build accepts them, and the hand-built HTTP/2, HTTP/3, TLS-SRP and session-file code
keeps running there.

`CurlCommandRunner` and `CurlComposition.CreateRunner` take an optional `parsesAsWindowsBuild`, and
`CommandLineParser.Parse` gains an overload with both a `DefaultConfigFileSearch` and `isWindows`, so
tests reach those features on every platform by reading as the OpenSSL build. `--ai-help` says of each
such option that Windows refuses it; the bytes are the same on every platform.

## Consequences

- `curl --http2` and the rest on Windows match the Schannel curl byte for byte; AF-0022's
  reproduction gives exit 2 for both binaries.
- On Windows the hand-built HTTP/2, HTTP/3 and TLS-SRP stacks are no longer reachable from the
  command line. They stay built, tested and used off Windows. An `Alt-Svc: h3` entry and the
  default `h2` ALPN offer, which do not need these options, are unaffected.
- Should the platform's curl gain one of these features, its row drops `RefusedBySchannelBuild`.

## Alternatives considered

- **Keep accepting them everywhere (ADR-0141 and the rest as they were).** A script written for
  the Windows curl that relies on the refusal, or whose output differs, could tell the binaries apart;
  Stewart accepted the finding that this is a defect.
- **Refuse them on every platform.** curl's OpenSSL builds on Linux and macOS accept them, so that
  would trade one difference for another.
- **A separate switch for the Schannel feature set.** The parser already has one notion of "read as
  the Windows build" (`ReadsArgumentsAsUtf8`); a second, independent switch would let the two
  disagree, which no real build does.
