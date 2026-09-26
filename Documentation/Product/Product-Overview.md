# Product Overview

- **Status:** Rough-out. Scope and architecture are drafted and sourced; the numbers
  are measured, not estimated. Sections marked `> **TODO**` still need a decision.
- **Last updated:** 2026-09-25
- **Measured against:** curl 8.21.0 (released 2026-06-24) and `curl/curl@master`

## What is Curl?

Curl is a drop-in replacement for the `curl` command-line tool, written in C# on
.NET 10. It aims to be indistinguishable from the original at the command line: the
same options, the same exit codes, the same bytes on stdout and stderr, so an
existing script cannot tell which binary it invoked.

It is not a wrapper around the original and not a port of its source. Every protocol
is implemented against its specification, in its own class library, behind
interfaces that can be driven from a unit test without touching a network.

## Problem

`curl` is the most widely deployed data-transfer tool in existence, and it is C.
That is the right choice for a universal binary and an awkward one for anybody who
wants to extend it, embed it in managed code, or reason about its behaviour under
test. Three concrete consequences:

- **Extending it means writing C.** A team whose stack is .NET cannot add a
  protocol, an auth scheme or an output format without leaving its toolchain.
- **Its protocol logic is hard to unit test in isolation.** curl's own suite is
  excellent but it is predominantly *integration* testing — 2,126 test-case files
  driving real server processes. Testing FTP response parsing without an FTP server
  is not something the architecture invites.
- **Embedding in .NET means P/Invoke.** Managed callers reach libcurl across a
  native interop boundary, with the marshalling, deployment and AOT friction that
  implies.

Today the alternatives are: run `curl` as an opaque subprocess, P/Invoke libcurl, or
hand-roll against `HttpClient` and accept that non-HTTP protocols are unavailable.

## Users

- **Script authors and operators** who want `curl` on PATH and never think about it
  again. They are the reason drop-in fidelity is the top requirement; they will
  never read this document, and the product succeeds if they never need to.
- **.NET developers** who want curl's protocol coverage — FTP, SFTP, SMTP, IMAP,
  MQTT, TFTP — from managed code, without interop.
- **Contributors** who want to add or fix a protocol. The one-library-per-protocol
  structure exists for them: a change to FTP cannot compile against HTTP internals.

## Non-goals

- **libcurl's C ABI.** Native programs that link `libcurl.so` / `libcurl.dll` are
  out of scope. Exporting a binary-compatible C ABI with matching struct layouts,
  `CURLOPT_*` numbering and callback signatures is a separate project, and
  attempting it would compromise the design of this one.
- **Beating curl on performance.** Parity is the target. C# will not win a
  byte-shuffling contest against twenty-five years of tuned C, and pretending
  otherwise would drive bad architectural choices.
- **Replacing curl.** This is an alternative implementation, not a fork and not a
  competitor. Upstream is the specification.
- **Protocols curl itself has dropped.** RTMP went when librtmp support was removed;
  it is not coming back here.

## Scope: the compatibility surface

Measured directly from curl 8.21.0 rather than estimated:

| Surface | Size | Source |
| --- | --- | --- |
| URL schemes | **29** | `curl --version` plus the upstream repository description |
| Command-line options | **274** | `curl --help all` |
| Option categories | **25** | `curl --help category` |
| `--write-out` variables | **76** | `docs/cmdline-opts/write-out.md` |
| Exit codes | **0–101**, with gaps | `libcurl-errors` (`CURLE_*`) |
| Upstream test cases | **2,126** | `tests/data` on `master` |

### The 29 schemes, and why they are 16 libraries

A scheme is not a protocol. Nine schemes differ from another only by transport
security, and two are not wire protocols at all. Folding those correctly is the
difference between 16 libraries and 29 with duplicated TLS in each.

| Library | Schemes covered |
| --- | --- |
| `Curl.Protocol.Http.UnitLibrary` | `http`, `https`, plus `ipfs`, `ipns` |
| `Curl.Protocol.Ws.UnitLibrary` | `ws`, `wss` |
| `Curl.Protocol.Ftp.UnitLibrary` | `ftp`, `ftps` |
| `Curl.Protocol.Ssh.UnitLibrary` | `scp`, `sftp` |
| `Curl.Protocol.Smtp.UnitLibrary` | `smtp`, `smtps` |
| `Curl.Protocol.Imap.UnitLibrary` | `imap`, `imaps` |
| `Curl.Protocol.Pop3.UnitLibrary` | `pop3`, `pop3s` |
| `Curl.Protocol.Ldap.UnitLibrary` | `ldap`, `ldaps` |
| `Curl.Protocol.Mqtt.UnitLibrary` | `mqtt`, `mqtts` |
| `Curl.Protocol.Smb.UnitLibrary` | `smb`, `smbs` |
| `Curl.Protocol.Gopher.UnitLibrary` | `gopher`, `gophers` |
| `Curl.Protocol.Rtsp.UnitLibrary` | `rtsp` |
| `Curl.Protocol.Tftp.UnitLibrary` | `tftp` |
| `Curl.Protocol.Telnet.UnitLibrary` | `telnet` |
| `Curl.Protocol.Dict.UnitLibrary` | `dict` |
| `Curl.Protocol.File.UnitLibrary` | `file` |

Two findings from the research drive that table:

- **TLS is a transport, not a protocol.** `https` is HTTP over TLS, and the same
  holds for `ftps`, `imaps`, `pop3s`, `smtps`, `ldaps`, `wss`, `smbs` and `gophers`.
  TLS therefore lives once, in `Curl.Networking.UnitLibrary`, and every protocol
  receives an already-secured stream. Implementing it per protocol would be nine
  copies of the hardest code in the system.
- **`ipfs` and `ipns` are URL rewrites, not a wire protocol.** curl resolves them to
  an HTTP gateway request, selecting the gateway from `--ipfs-gateway`, then the
  `IPFS_GATEWAY` environment variable, then `~/.ipfs/gateway`. They belong to URL
  handling and HTTP; giving them a library would model curl wrongly.

### Also in scope

- **HTTP versions:** HTTP/1.0, HTTP/1.1, HTTP/2, HTTP/3 over QUIC.
- **Authentication:** Basic, Digest, NTLM, Negotiate/SPNEGO/Kerberos, Bearer,
  AWS SigV4.
- **Proxies:** HTTP CONNECT, HTTPS proxy, `socks4`, `socks4a`, `socks5`, `socks5h`.
- **Configuration:** `.curlrc`, `-K`/`--config` files, `--variable` expansion.
- **Behavioural surface:** progress meter, `-v`/`--trace`/`--trace-ascii`,
  `--write-out` including `%{json}` and `%{header{name}}`, cookie jars with Public
  Suffix List handling, `alt-svc`, HSTS, resume, rate limiting, retries.

## Architecture

Two rules carry the whole design. Everything else is detail.

### Rule 1 — protocols depend on abstractions, never on each other

`Curl.Protocol.Abstractions.UnitLibrary` holds the contracts: `IProtocolHandler`,
`IConnection`, `IDnsResolver`, `ITlsProvider`, `ITransferContext`. Every protocol
library references that and nothing else horizontal. No protocol library may
reference another.

This is checkable, so it gets checked: a test in
`Curl.Protocol.Abstractions.UnitTests` asserts the reference graph and CI fails on a
violation. Modularity that is only a convention decays on contact with a deadline.

### Rule 2 — the transport is an injected seam

Protocol handlers never construct a `Socket`, an `SslStream` or an `HttpClient`.
They receive `IConnection` — except `file`, which has no wire and receives
`IFileSystem` instead (`Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`).
That single decision is what makes "deeply unit testable" true rather than
aspirational:

```
FtpProtocolHandler(IConnection, IDnsResolver, TimeProvider)
        │
        ├─ unit test  → FakeConnection replaying recorded bytes.
        │               No network. No server. No [TestCategory("Integration")].
        │
        └─ production → SocketConnection, wrapped by SslStream when the
                        scheme is secure.
```

Every protocol's wire behaviour — FTP's `227` PASV reply parsing, SMTP's multiline
continuations, chunked transfer decoding, MQTT packet framing — becomes an assertion
over a byte array. `TimeProvider` is injected for the same reason, so timeout and
retry logic is testable without `Thread.Sleep`.

**TLS is the deliberate exception.** `Curl.Networking.UnitLibrary` uses .NET's
`SslStream` rather than implementing the handshake. Writing new TLS is how projects
introduce security holes; this one is not going to. Raw sockets sit above and below
`SslStream`, never inside it.

### Layers

| Project | Responsibility |
| --- | --- |
| `Curl.Console` | Entry point. `AssemblyName` is `curl`, published native-AOT single-file so it drops onto PATH as `curl.exe`. |
| `Curl.Cli.UnitLibrary` | The 274-option table, argument parsing, `.curlrc` and `-K`, `--variable`, usage text, exit-code mapping. |
| `Curl.Core.UnitLibrary` | Transfer engine: URL parsing, scheme dispatch, redirects, resume, retries, rate limiting, IPFS gateway rewriting. |
| `Curl.Networking.UnitLibrary` | `IConnection`/`IDnsResolver`/`ITlsProvider` implementations, TLS via `SslStream`, proxy and SOCKS handling, connection reuse. |
| `Curl.Authentication.UnitLibrary` | Basic, Digest, NTLM, Negotiate, Bearer, AWS SigV4. |
| `Curl.Cookies.UnitLibrary` | Cookie jar, Netscape file format, Public Suffix List. |
| `Curl.Output.UnitLibrary` | The 76 `--write-out` variables, progress meter, verbose and trace formatting. |
| `Curl.Protocol.Abstractions.UnitLibrary` | Contracts. Depends on nothing. |
| `Curl.Protocol.*.UnitLibrary` | One per protocol family, 16 in total. |

Dependencies point one way — `Console → Cli → Core → {Protocols} → Abstractions` —
with `Networking`, `Authentication`, `Cookies` and `Output` injected as services.
Nothing points back up.

## Project layout

Flat and linear. Every project is a directory immediately under the repository root
— no `src/`, no `tests/`, no grouping folders — and `Curl.slnx` lists them as one
unbroken run with no solution folders around them. Because the names sort that way,
each `.UnitTests` project sits directly after the `.UnitLibrary` it tests.

**24 production projects, 24 test projects, 48 in total.**

```
Curl.Authentication.UnitLibrary/          Curl.Protocol.Gopher.UnitLibrary/
Curl.Authentication.UnitTests/            Curl.Protocol.Gopher.UnitTests/
Curl.Cli.UnitLibrary/                     Curl.Protocol.Http.UnitLibrary/
Curl.Cli.UnitTests/                       Curl.Protocol.Http.UnitTests/
Curl.Console/                             Curl.Protocol.Imap.UnitLibrary/
Curl.Console.UnitTests/                   Curl.Protocol.Imap.UnitTests/
Curl.Cookies.UnitLibrary/                 Curl.Protocol.Ldap.UnitLibrary/
Curl.Cookies.UnitTests/                   Curl.Protocol.Ldap.UnitTests/
Curl.Core.UnitLibrary/                    Curl.Protocol.Mqtt.UnitLibrary/
Curl.Core.UnitTests/                      Curl.Protocol.Mqtt.UnitTests/
Curl.Networking.UnitLibrary/              Curl.Protocol.Pop3.UnitLibrary/
Curl.Networking.UnitTests/                Curl.Protocol.Pop3.UnitTests/
Curl.Output.UnitLibrary/                  Curl.Protocol.Rtsp.UnitLibrary/
Curl.Output.UnitTests/                    Curl.Protocol.Rtsp.UnitTests/
Curl.Protocol.Abstractions.UnitLibrary/   Curl.Protocol.Smb.UnitLibrary/
Curl.Protocol.Abstractions.UnitTests/     Curl.Protocol.Smb.UnitTests/
Curl.Protocol.Dict.UnitLibrary/           Curl.Protocol.Smtp.UnitLibrary/
Curl.Protocol.Dict.UnitTests/             Curl.Protocol.Smtp.UnitTests/
Curl.Protocol.File.UnitLibrary/           Curl.Protocol.Ssh.UnitLibrary/
Curl.Protocol.File.UnitTests/             Curl.Protocol.Ssh.UnitTests/
Curl.Protocol.Ftp.UnitLibrary/            Curl.Protocol.Telnet.UnitLibrary/
Curl.Protocol.Ftp.UnitTests/              Curl.Protocol.Telnet.UnitTests/
                                          Curl.Protocol.Tftp.UnitLibrary/
                                          Curl.Protocol.Tftp.UnitTests/
                                          Curl.Protocol.Ws.UnitLibrary/
                                          Curl.Protocol.Ws.UnitTests/
```

`Curl.Console` carries no `.UnitLibrary` suffix because it is an executable, not a
library. It is the only exception.

## Success criteria

Checkable by someone outside the project, in priority order:

1. **Upstream conformance.** curl's own 2,126 test cases are the oracle. A stated
   pass rate against them, rising per release, is the headline number — far better
   evidence than any coverage percentage.
2. **Differential testing.** For a corpus of invocations, real `curl` and this
   implementation produce byte-identical stdout, stderr and exit code. A
   disagreement is a bug here until proven otherwise.
3. **Exit-code fidelity.** Every `CURLE_*` value this implementation can reach is
   returned in the same circumstances as upstream.
4. **Unit tests need no network.** `dotnet test --filter "TestCategory!=Integration"`
   passes with networking disabled. If a protocol needs a live server to test, the
   seam is in the wrong place.
5. **Drops onto PATH.** A published `curl.exe` can replace the system binary and
   existing scripts keep working unchanged.

## Constraints

- **Runtime:** .NET 10, `net10.0`. Nullable enabled, warnings as errors.
- **Publish:** native AOT, single file. This rules out reflection-heavy designs and
  is a reason dependency injection is wired explicitly rather than by scanning.
- **Platform:** Windows is the development platform. Linux and macOS parity is
  wanted; > **TODO** confirm whether it is a release requirement.
- **Licensing — needs a decision.** This repository is **GPL-3.0**. curl is under
  the **curl licence**, which is permissive and *GPL-compatible*, so incorporating
  upstream code into a GPL-3.0 project is legally permissible provided copyright
  notices are preserved. Two things still follow:
  - **Clean-room is a choice worth making anyway.** Building from RFCs, the man page
    and observable behaviour — rather than translating C — avoids licence-provenance
    auditing and keeps the codebase unambiguously ours. Adopted as the working rule.
  - **GPL-3.0 works against the goal.** curl is everywhere *because* it is
    permissive. Copyleft blocks the embed-in-anything use that makes a drop-in
    replacement worth having. MIT or Apache-2.0 would serve the product better.
    > **TODO** confirm GPL-3.0 is intentional, or relicense before first release.

## Phasing

Full scope is the target; it is not one push. Each phase must leave the solution
building and its tests green.

| Phase | Delivers | Proves |
| --- | --- | --- |
| **0** | Solution, documentation, conventions | — (complete) |
| **1** | `Abstractions`, `Networking`, `Core`, `Cli`, `Output`, `Console`, plus `File` and `Http` | The architecture end to end. `curl https://…` works and the seam holds. |
| **2** | `Ftp`, `Ssh`, `Authentication`, `Cookies` | A second transport shape — control plus data channels — does not distort the design. |
| **3** | `Smtp`, `Imap`, `Pop3` | Line-oriented protocols share machinery cleanly. |
| **4** | `Ws`, `Mqtt`, `Tftp`, `Dict`, `Gopher`, `Telnet` | Breadth. |
| **5** | `Ldap`, `Smb`, `Rtsp` | The awkward remainder. |
| **6** | Upstream conformance push, HTTP/3, AOT publish | The drop-in claim becomes defensible. |

Projects are created as their phase begins, not all 48 up front. Forty-eight empty
projects before anything works is a liability, not a head start.

## Open questions

| # | Question | Blocks |
| --- | --- | --- |
| 1 | Is GPL-3.0 intentional, or should this relicense to MIT/Apache-2.0 before release? | First public release |
| 2 | Are Linux and macOS release requirements, or is this Windows-first? | Phase 1 CI design |
| 3 | How is curl's test suite driven from .NET — port the harness, or run upstream's Perl `runtests.pl` against our binary? | Phase 1 conformance work |
| 4 | Is a managed NuGet API a deliverable, or is the CLI the only product? | Public API surface |
| 5 | HTTP/2 and HTTP/3: BCL framing, or implemented over the seam like everything else? | Phases 1 and 6 |

## Sources

- `curl --version` and `curl --help all`, curl 8.21.0 (2026-06-24), Schannel build
- <https://curl.se/docs/manpage.html> — options and the DESCRIPTION protocol list
- <https://curl.se/docs/url-syntax.html> and `docs/URL-SYNTAX.md` — scheme syntax
- <https://curl.se/libcurl/c/libcurl-errors.html> — `CURLE_*` codes 0–101
- <https://curl.se/docs/copyright.html> — the curl licence
- <https://curl.se/docs/ipfs.html> — IPFS/IPNS gateway rewriting
- `docs/cmdline-opts/write-out.md` — the 76 `--write-out` variables
- `tests/data` on `curl/curl@master` — 2,126 test-case files
