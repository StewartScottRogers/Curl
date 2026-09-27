# Architecture Decision Records

One file per decision, named `ADR-NNNN-short-slug.md`. Numbers are assigned in
order and never reused.

An ADR is immutable once **Accepted**. A decision that changes does not get edited
— a new ADR supersedes it, and the old one is marked `Superseded by ADR-NNNN`.
The value of the record is that it shows what was believed *at the time*.

Write one when a choice is expensive to reverse, when a reasonable person would
have chosen differently, or when the reasoning would otherwise be lost. Routine
choices do not need one.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](ADR-0001-adopt-slnx-solution-format.md) | Adopt the `.slnx` solution format and a shared project for documentation | Accepted | 2026-09-25 |
| [0002](ADR-0002-ifilesystem-as-the-second-protocol-seam.md) | `IFileSystem` as the second protocol seam | Accepted | 2026-09-25 |
| [0003](ADR-0003-itransfercontext-carries-transfer-options.md) | `ITransferContext` carries transfer options | Accepted | 2026-09-25 |
| [0004](ADR-0004-upload-file-name-percent-encoded-as-utf8.md) | The `-T` file name appended to a URL is percent-encoded as UTF-8 | Proposed | 2026-09-26 |
| [0005](ADR-0005-protocol-handlers-acquire-transports-through-connectors.md) | Protocol handlers acquire transports through connectors | Accepted | 2026-09-26 |
| [0006](ADR-0006-transfer-context-carries-phase-4-protocol-options.md) | The transfer context carries the Phase 4 protocol options | Accepted | 2026-09-26 |
| [0007](ADR-0007-file-handler-keeps-negative-resume-guard.md) | `FileProtocolHandler` keeps its negative-`ResumeFrom` guard as an unreachable defensive default | Accepted | 2026-09-26 |
| [0008](ADR-0008-transfer-context-carries-connect-timeout-and-max-time.md) | The transfer context carries the connect timeout and the maximum time | Accepted | 2026-09-26 |
| [0009](ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md) | TLS failure messages, `--capath`, `--cert` formats and the trust store match the platform's usual curl build | Accepted | 2026-09-26 |
| [0010](ADR-0010-representing-urls-system-uri-cannot-round-trip.md) | Representing URLs `System.Uri` cannot round-trip: replace, wrap or pre-parse | Accepted | 2026-09-26 |
| [0011](ADR-0011-cipher-options-follow-schannel-on-windows-and-are-honoured-elsewhere.md) | `--ciphers` and `--tls13-ciphers`: Schannel behaviour on Windows, honoured through `CipherSuitesPolicy` on Linux and macOS | Accepted | 2026-09-26 |
| [0012](ADR-0012-relicense-from-gpl-3-0-to-mit.md) | Relicense from GPL-3.0 to MIT | Accepted | 2026-09-26 |
| [0013](ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md) | curl's upstream test cases run as data-driven MSTest cases, in process | Accepted | 2026-09-26 |
| [0014](ADR-0014-http-request-options-and-the-auth-cookie-and-proxy-seams.md) | HTTP request options and the auth, cookie and proxy seams | Accepted | 2026-09-26 |
| [0015](ADR-0015-transfer-report-on-transfer-result-and-connect-timings-on-connect-result.md) | `TransferReport` on `TransferResult` and `ConnectTimings` on `ConnectResult` | Accepted | 2026-09-26 |
| [0016](ADR-0016-authentication-and-cookies-move-into-milestone-1.md) | `Curl.Authentication` and `Curl.Cookies` move into Milestone 1 | Accepted | 2026-09-26 |
| [0017](ADR-0017-no-http-2-or-http-3-in-milestone-1.md) | No HTTP/2 or HTTP/3 in Milestone 1: `--http2`, `--http2-prior-knowledge` and `--http3` are refused as the reference build refuses them | Accepted | 2026-09-26 |
| [0018](ADR-0018-the-mingw-curl-8-21-0-build-is-the-windows-http-reference.md) | The mingw build of curl 8.21.0 is the Windows reference for HTTP; Curl may differ from System32 `curl.exe` only where that build lacks a feature | Accepted | 2026-09-26 |
| [0019](ADR-0019-numeric-option-ceiling-matches-the-platform-curl.md) | The ceiling of a numeric option matches the platform curl: 2^31-1 on Windows, 2^63-1 on Linux and macOS | Accepted | 2026-09-26 |
| [0020](ADR-0020-compressed-advertises-deflate-gzip-and-br-until-a-zstd-decoder-exists.md) | `--compressed` advertises `deflate, gzip, br` until a zstd decoder exists | Accepted | 2026-09-26 |
| [0021](ADR-0021-v-version-keeps-curls-format-and-lists-only-what-curl-implements.md) | `-V`/`--version` keeps curl's format and version number and lists only what Curl implements, on each platform | Accepted | 2026-09-26 |
| [0022](ADR-0022-basic-and-bearer-credentials-are-sent-as-the-platform-curl-sends-them.md) | Basic and Bearer credentials are sent as the platform curl sends them: the ANSI code page on Windows, UTF-8 elsewhere, pre-emptively only for exactly one scheme | Accepted | 2026-09-26 |
| [0023](ADR-0023-the-connector-tunnels-through-an-http-proxy-as-curl-8-21-0-does.md) | The connector tunnels through an HTTP proxy as curl 8.21.0 does: measured CONNECT bytes, exit 7/56/5 as measured, HTTPS and SOCKS proxies not yet | Accepted | 2026-09-26 |
| [0024](ADR-0024-proxy-selection-follows-the-platform-curl-and-reads-an-injected-environment.md) | Proxy selection follows the platform curl and reads an injected environment | Accepted | 2026-09-26 |
| [0025](ADR-0025-digest-answers-as-curls-own-digest-code-on-every-platform.md) | Digest answers as curl's own Digest code does on every platform, not as Windows' WDigest does for the Schannel build | Accepted | 2026-09-26 |
| [0026](ADR-0026-auth-scheme-and-proxy-options-parse-as-curls-tool-keeps-them.md) | The auth-scheme and proxy options parse as curl's tool keeps them: one scheme bit set, one proxy slot, an unknown proxy scheme fails the transfer with exit 7 | Accepted | 2026-09-26 |
| [0027](ADR-0027-multipart-form-bodies-are-built-as-libcurl-8-21-0-builds-them.md) | Multipart form bodies are built as libcurl 8.21.0 builds them: measured headers and bytes, files streamed, exit 26 before connecting | Accepted | 2026-09-26 |
| [0028](ADR-0028-auth-scheme-ranking-follows-libcurl-with-no-fallback.md) | The auth scheme is ranked as libcurl ranks it - Negotiate, Bearer, Digest, NTLM, Basic - with no fallback from a scheme not built | Accepted | 2026-09-26 |
| [0029](ADR-0029-output-options-pair-with-urls-as-curls-tool-pairs-them.md) | The output options pair with URLs as curl's tool pairs them: one list of URL and output entries, `--remote-name-all` a creation-time default, `-w @file` read as `-d @file` is | Accepted | 2026-09-26 |
| [0030](ADR-0030-connect-timings-are-taken-by-the-connector-with-the-handshake-from-the-tls-provider.md) | Connect timings are taken by the connector, with the handshake's end from the TLS provider; the dialer reports the local end point | Accepted | 2026-09-26 |
| [0031](ADR-0031-corrupt-compressed-bodies-report-curls-generic-exit-61-text-where-zlib-text-is-unavailable.md) | Corrupt `--compressed` bodies report curl's generic exit 61 text where zlib's own text is unavailable | Accepted | 2026-09-26 |
| [0032](ADR-0032-url-globs-expand-as-curl-8-21-0s-tool-expands-them.md) | URL globs expand as curl 8.21.0's tool expands them: measured messages and columns, lazy expansion, `#N` as `glob_match_url` | Accepted | 2026-09-26 |
| [0033](ADR-0033-a-followed-put-redirect-rewinds-a-seekable-upload-and-passes-stdin-on-as-it-is.md) | A followed PUT redirect rewinds a seekable `-T` upload and passes standard input on as it is | Accepted | 2026-09-26 |
| [0034](ADR-0034-http-authentication-retries-a-401-once-as-curl-8-21-0-does.md) | HTTP authentication retries a 401 once, as curl 8.21.0 does | Accepted | 2026-09-26 |
| [0035](ADR-0035-w-times-print-microseconds-since-the-start-and-speeds-divide-by-time-total.md) | `-w` times print microseconds since the start, and speeds divide by `time_total` | Accepted | 2026-09-26 |
| [0036](ADR-0036-http-request-bodies-are-framed-and-counted-as-curl-8-21-0-frames-them.md) | HTTP request bodies are framed and counted as curl 8.21.0 frames them: `--json` headers after every `-H`, a failed read ends a chunked body, an early final status is the response, `RequestSize` includes the body | Accepted | 2026-09-26 |
| [0037](ADR-0037-a-z-value-that-is-not-a-date-is-read-as-a-file-through-idatafilereader.md) | A `-z` value that is not a date is read as a file through `IDataFileReader`, with curl's Windows filetime warning | Accepted | 2026-09-26 |
| [0038](ADR-0038-w-time-follows-the-windows-c-runtime-strftime-and-onerror-reads-transfer-failed.md) | `-w %time{…}` follows the Windows C runtime's `strftime` with en-US names, and `%{onerror}` reads `IWriteOutVariableSource.TransferFailed` | Accepted | 2026-09-26 |
| [0039](ADR-0039-a-64-bit-numeric-option-value-saturates-where-commandlineoptions-holds-less.md) | A 64-bit numeric option value saturates where `CommandLineOptions` holds less: `--tftp-blksize` and `--max-redirs` at 2^31-1, the timeouts at the longest `TimeSpan` | Accepted | 2026-09-26 |
| [0040](ADR-0040-http-enforces-max-time-and-connect-timeout-in-the-handler.md) | The HTTP handler enforces `-m` and `--connect-timeout` itself on the transfer's clock, and maps failed sends to exit 55 | Accepted | 2026-09-26 |
| [0041](ADR-0041-multipart-encoder-parts-are-encoded-whole-while-the-body-is-built.md) | Multipart `;encoder=` parts are encoded whole while the body is built: `binary` and `8bit` files still stream, `base64`, `quoted-printable` and `7bit` are read and encoded in memory, and a `7bit` refusal is reported after every other part | Accepted | 2026-09-26 |
| [0042](ADR-0042-the-sws-emulation-carries-out-timing-commands-on-an-injected-clock-and-leaves-delay-unsupported.md) | The sws emulation carries out `idle`, `stream`, `writedelay`, `connection-monitor`, `upgrade` and `<postcmd>` `wait` on an injected clock, and leaves `delay` unsupported | Accepted | 2026-09-26 |

## Template

```markdown
# ADR-NNNN — <title>

- **Status:** Proposed / Accepted / Superseded by ADR-NNNN
- **Date:** YYYY-MM-DD

## Context
The forces at play. What made a decision necessary.

## Decision
What was chosen, stated as a decision rather than a description.

## Consequences
What this makes easy, and what it makes hard. Both, honestly.

## Alternatives considered
Each option and the specific reason it lost.
```
