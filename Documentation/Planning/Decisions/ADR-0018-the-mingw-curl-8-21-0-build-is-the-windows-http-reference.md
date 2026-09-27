# ADR-0018 — The mingw build of curl 8.21.0 is the Windows reference for HTTP; Curl may differ from System32 `curl.exe` only where that build lacks a feature

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

[ADR-0009](ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md) makes the
Schannel build of curl 8.21.0 the Windows reference, and measures it as
`/mingw64/bin/curl`, but it decides TLS behaviour only. The Phase 1 HTTP plan
(protocol-architect, 2026-09-26, item D3) needs the same answer for HTTP, because two
Windows builds of curl 8.21.0 are installed and they do not offer the same features.
Content decoding (`--compressed`), NTLM authentication, the public suffix list for
cookies, and the `rtsp`, `scp` and `sftp` schemes all depend on which one is the
reference; so does the `curl -V` text (BL-155).

Measured 2026-09-26 with `curl -V` on the measuring host, where `/mingw64/bin/curl` is
first on PATH:

**`/mingw64/bin/curl`**

```
curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP
Release-Date: 2026-06-24
Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss
Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
```

**`C:\Windows\System32\curl.exe`**

```
curl 8.21.0 (Windows) libcurl/8.21.0 Schannel zlib/1.3.2 WinIDN WinLDAP
Release-Date: 2026-06-24
Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s smtp smtps telnet tftp ws wss
Features: alt-svc AsynchDNS HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz SPNEGO SSL SSPI threadsafe Unicode UnixSockets
```

Compared with the mingw build, System32 `curl.exe` 8.21.0 lacks:

| Kind | Missing from System32 `curl.exe` |
| --- | --- |
| Protocols | `rtsp`, `scp`, `sftp` |
| Features | `brotli`, `zstd`, `NTLM`, `PSL` |

Neither build lists `HTTP2` or `HTTP3` (see
[ADR-0017](ADR-0017-no-http-2-or-http-3-in-milestone-1.md)). System32 `curl.exe` lists
one feature the mingw build does not, `Unicode`, and uses WinIDN where the mingw build
uses libidn2; both list `IDN`.

Curl's roadmap implements brotli and zstd decoding, NTLM, the public suffix list and
the `rtsp`, `scp` and `sftp` schemes, so only the fuller build describes what Curl
will do.

## Decision

1. The Windows reference for HTTP behaviour is the mingw build of curl 8.21.0,
   `/mingw64/bin/curl` (`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel …`),
   the same binary ADR-0009 measured for TLS. Request bytes, response handling, output
   bytes, exit codes and `--write-out` values are measured against it and pinned from
   it.
2. Curl may differ from System32 `curl.exe` 8.21.0 exactly where that build lacks a
   feature or protocol the mingw build has: `rtsp`, `scp`, `sftp`, `brotli`, `zstd`,
   `NTLM` and `PSL`. There Curl behaves like the mingw build; for example
   `--compressed` offers and decodes `br` and `zstd`, and an `sftp://` URL is
   transferred rather than refused as an unsupported protocol.
3. Everywhere else, the two builds are expected to agree, and a difference between
   Curl and System32 `curl.exe` is a defect, except for text a build prints about
   itself (for example the `curl -V` version line, or the "try 'curl --help'" line
   ADR-0017 records).

## Consequences

- Curl on Windows is a superset of System32 `curl.exe`: a script written for System32
  `curl.exe` keeps working, and a script written for the mingw build does too.
- A script that relies on System32 `curl.exe` refusing a feature (for example
  `--compressed` not sending `br`, or `sftp://` failing with exit 1) sees different
  behaviour. This is deliberate and follows from this decision.
- Conformance measurements on Windows run `/mingw64/bin/curl`, not whichever `curl` a
  shell resolves first; tests and audits name the binary they measured.
- `curl -V` output (BL-155) is modelled on the mingw build's lines, restricted to what
  Curl actually implements.

## Alternatives considered

1. **System32 `curl.exe` as the reference.** Lost: it is the curl every Windows 10 and
   11 machine has, but it lacks features Curl's roadmap implements, so Curl would
   either drop those features or diverge from its own reference everywhere it has
   them.
2. **Match each build depending on how Curl is invoked or installed.** Lost: one
   binary cannot know which curl a script was written for, and two behaviours double
   the tests for no user benefit.
