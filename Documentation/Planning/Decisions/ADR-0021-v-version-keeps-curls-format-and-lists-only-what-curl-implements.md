# ADR-0021 — `-V`/`--version` keeps curl's format and version number and lists only what Curl implements, on each platform

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

The Phase 1 HTTP plan (protocol-architect, 2026-09-26, item D5) needs the exact text
`-V`/`--version` prints before the task that prints it (BL-200) can be written. Scripts
read this text in two ways: they parse the version number off line 1 to decide what
options they may use, and they grep `Protocols:` and `Features:` to feature-detect
(for example `curl -V | grep -q HTTPS-proxy`). Both uses must keep working, and the
second must get true answers.

The reference is the mingw build of curl 8.21.0
([ADR-0018](ADR-0018-the-mingw-curl-8-21-0-build-is-the-windows-http-reference.md)).
Measured 2026-09-26 on the Windows measuring host with `/mingw64/bin/curl -V`:

```
curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP
Release-Date: 2026-06-24
Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss
Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
```

Also measured: the four lines end in CRLF (`\r\n`, four of them, 464 bytes in all),
`--version` writes exactly the same bytes as `-V`, and the exit code is 0.

The format is documented in the curl manual under `-V, --version`
(<https://curl.se/docs/manpage.html>, checked against curl 8.21.0): line 1 is the curl
version, the libcurl version and the third-party libraries it is built with; then the
release date; then `Protocols:` (the protocols libcurl supports, in alphabetical order)
and `Features:` (the features libcurl supports).

Curl contains none of the libraries on the measured line 1 except the TLS backend the
.NET base class library sits on: `SslStream` uses Schannel on Windows and OpenSSL on
Linux ([ADR-0009](ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md)),
and Apple's Security framework on macOS. It implements only some of the listed
protocols and features, as the survey below shows.

### What Curl implements on 2026-09-26

**Protocols.** `CurlComposition.CreateProtocolHandlers` in
`Curl.Console/CurlComposition.cs` registers six handlers. The schemes each claims, from
its `SupportedSchemes`:

| Handler | Source | Schemes |
| --- | --- | --- |
| `FileProtocolHandler` | `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` | `file` |
| `DictProtocolHandler` | `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs` | `dict` |
| `GopherProtocolHandler` | `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` | `gopher`, `gophers` (TLS when the scheme is `gophers`) |
| `TelnetProtocolHandler` | `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs` | `telnet` |
| `TftpProtocolHandler` | `Curl.Protocol.Tftp.UnitLibrary/TftpProtocolHandler.cs` | `tftp` |
| `MqttProtocolHandler` | `Curl.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs` | `mqtt`, `mqtts` (TLS when the scheme is `mqtts`) |

`HttpProtocolHandler` claims `http` and `https` but is not registered, so neither is
served and neither is listed.

**Features.** A feature is listed only where the code gives evidence for it:

| curl feature | Listed | Evidence |
| --- | --- | --- |
| `AsynchDNS` | Yes | `SystemDnsResolver` (`Curl.Networking.UnitLibrary`) resolves through `Dns.GetHostAddressesAsync` and takes a `CancellationToken`; `TcpConnector` and `UdpDatagramConnector` await it, so resolution does not block the transfer and can be cut off. |
| `IPv6` | Yes | `SystemDnsResolver` returns every address family; `TcpDialer` opens its socket in the endpoint's `AddressFamily`; `UdpDatagramChannel` binds `IPAddress.IPv6Any` for an IPv6 peer; the handlers connect to `Uri.IdnHost`, which strips the brackets of an IPv6 literal (`TelnetProtocolHandlerTests` pins `telnet://[::1]:2323/` to host `::1`). |
| `Largefile` | Yes | Sizes and offsets are 64-bit: `ITransferContext.ResumeFrom` and `MaxFileSize` are `long?`, `ByteRange` bounds are `long`, and `FileProtocolHandler` copies through `Stream`, whose lengths and positions are `long`. |
| `SSL` | Yes | `gophers` and `mqtts` are served over TLS through `SslStreamTlsProvider`, built in `CurlComposition.CreateTransports`. |
| `libz`, `brotli`, `zstd` | No | No registered handler decodes a content encoding; decoding belongs to HTTP (ADR-0020), which is not registered. |
| `IDN` | No | Handlers pass `Uri.IdnHost`, but no test pins an internationalised host, and curl's IDN is libidn2's IDNA 2008, which `System.Uri` is not shown to match. |
| `threadsafe` | No | It describes libcurl's global initialisation being thread-safe; Curl exposes no libcurl API for it to be true of. |
| `UnixSockets` | No | No `--unix-socket` or `--abstract-unix-socket` support and no Unix domain socket dialer. |
| `alt-svc`, `HSTS`, `HTTPS-proxy`, `Kerberos`, `NTLM`, `PSL`, `SPNEGO`, `SSPI` | No | HTTP-side features; the HTTP handler is not registered. |

## Decision

1. `-V` and `--version` print the same four lines and exit 0. Each line ends in CRLF on
   Windows, as measured, and in LF on Linux and macOS, as each platform's build writes
   it (to confirm against an OpenSSL build of curl 8.21.0).
2. **Version number and format are curl's.** Line 1 is
   `curl 8.21.0 (<triple>) libcurl/8.21.0` followed by the TLS backend token, the
   second line is `Release-Date: 2026-06-24`, and the third and fourth are
   `Protocols:` and `Features:` with single-space separators. The version and release
   date are the reference's, so a script that gates on the version keeps working.
3. **Line 1 names only a library Curl uses.** That is the TLS backend `SslStream` sits
   on, with no version number: `Schannel` on Windows (curl prints Schannel without a
   version; measured), `OpenSSL` on Linux, `SecureTransport` on macOS. No `zlib`,
   `brotli`, `zstd`, `libidn2`, `libpsl`, `libssh2` or `WinLDAP` token is printed,
   because Curl contains none of those libraries: the BCL's `ZLibStream` and
   `BrotliStream` are not zlib 1.3.2 or brotli 1.2.0, and printing their version
   strings would state something false.
   - **Linux:** curl prints `OpenSSL/<version>`. Curl prints `OpenSSL` bare: the
     version is that of whichever `libssl` the host has, the BCL exposes no API for it,
     and a hard-coded version would be false on most hosts. `grep OpenSSL` still
     matches.
   - **macOS:** `SslStream` uses Apple's Security framework. curl's name for a backend
     on that framework was `SecureTransport`; it is the only true name curl has used,
     so Curl prints it. curl dropped its Secure Transport backend in 8.15.0, so no
     curl 8.21.0 build prints this token; it is kept because the alternatives are
     false (`OpenSSL`) or hide the TLS support `SSL` announces (nothing).
4. **`Protocols:` lists exactly the schemes the registered handlers serve,** in curl's
   alphabetical order. On 2026-09-26: `dict file gopher gophers mqtt mqtts telnet tftp`.
5. **`Features:` lists exactly the features with evidence in the code** (Context
   table), in curl's order, which is alphabetical ignoring case. On 2026-09-26:
   `AsynchDNS IPv6 Largefile SSL`.
6. **Keeping the lists current.** The task that registers a handler in
   `CurlComposition.CreateProtocolHandlers`, or lands a feature curl lists, adds the
   scheme or feature to the printed lists, and to the test that pins them, in the same
   change. A handler or feature is never listed before it is served, and never left
   out once it is. When a line changes, this ADR's lines below are the 2026-09-26
   state, not the current one; the test in `Curl.Cli.UnitTests` is the current one.
7. BL-200 implements this decision.

### Exact lines, 2026-09-26

**Windows x64** (triple as the mingw reference prints it; measured):

```
curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel
Release-Date: 2026-06-24
Protocols: dict file gopher gophers mqtt mqtts telnet tftp
Features: AsynchDNS IPv6 Largefile SSL
```

**Linux x64** (triple and `OpenSSL` token: to confirm against an OpenSSL build of curl
8.21.0; the triple is the one ADR-0009 measured on curl 8.18.0, Ubuntu):

```
curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL
Release-Date: 2026-06-24
Protocols: dict file gopher gophers mqtt mqtts telnet tftp
Features: AsynchDNS IPv6 Largefile SSL
```

**macOS arm64** (triple and `SecureTransport` token: to confirm against an OpenSSL
build of curl 8.21.0; no macOS host was available, and curl prints the Darwin version
of the host it was built on, taken here as macOS 26):

```
curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport
Release-Date: 2026-06-24
Protocols: dict file gopher gophers mqtt mqtts telnet tftp
Features: AsynchDNS IPv6 Largefile SSL
```

The triple is a fixed string per published runtime identifier (`win-x64`, `linux-x64`,
`osx-arm64`), as curl's is fixed at build time; it is not derived from the running OS
version.

## Consequences

- A script that parses the version off line 1 sees `8.21.0` and behaves as it does
  with the reference.
- A script that feature-detects gets true answers: it is never told Curl speaks
  `https` or decodes `brotli` before Curl does, so it never picks a path Curl would
  fail.
- The output differs from the reference's by design, and keeps differing until every
  protocol and feature lands; a test that compares `-V` output byte for byte with real
  curl fails, and is expected to.
- Every task that registers a handler or lands a listed feature has one more thing to
  change, and a reviewer has one more thing to check. The pinning test in
  `Curl.Cli.UnitTests` makes a forgotten update visible only if the task also updates
  that test, so the rule in Decision 6 is what keeps the lists honest.
- The Linux and macOS lines are unmeasured in part. When an OpenSSL build of curl
  8.21.0 is measured and its triple or backend token differs, a new ADR supersedes
  this one for that line.
- `SecureTransport` on macOS is a token no curl 8.21.0 prints; a script that expects
  `OpenSSL` there sees something else.

## Alternatives considered

- **Print the reference's four lines verbatim.** Byte-identical, but it tells a
  feature-detecting script that Curl speaks `https`, `sftp` and `ldap` and decodes
  `zstd`; the script then fails where it would have taken another path. False
  documentation of behaviour, which the root `CLAUDE.md` treats as a defect.
- **Model the format on System32 `curl.exe`.** It prints
  `curl 8.21.0 (Windows) libcurl/8.21.0 Schannel zlib/1.3.2 WinIDN WinLDAP` on line 1.
  Lost: ADR-0018 makes the mingw build the Windows reference, and the triple form is
  also what the Linux and macOS builds print, so one format serves all three platforms.
- **Curl's own version number (for example `curl 0.1.0`).** Truthful about the binary,
  but every script that gates on `curl -V` version breaks, and Curl stops being a
  drop-in replacement.
- **Print `zlib`, `brotli` library tokens for the BCL's decoders.** They are not those
  libraries; the version strings would be invented. Left out until the tokens can be
  true, which they cannot be under the base-class-library-only rule.
- **`OpenSSL/<version>` on Linux, read from `libssl` at run time.** True, but needs
  native interop that the BCL does not provide as an API, for a version number few
  scripts read. Revisited if a script is found that needs it.
- **`OpenSSL` on macOS, to match the OpenSSL build ADR-0009 names as the macOS
  reference.** False: `SslStream` on macOS does not use OpenSSL.
- **No TLS token on macOS.** Hides the TLS support that `SSL` in `Features:` and
  `gophers`/`mqtts` in `Protocols:` announce.
- **Derive `Protocols:` from the registered handlers at run time.** Always current, but
  it couples the CLI library to the composition root, and curl's list also depends on
  build options Curl does not have; a pinned list with the Decision 6 rule is simpler
  and is what BL-200 is scoped to (`Curl.Cli.UnitLibrary` only).
