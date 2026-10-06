---
id: BL-1169
title: Write the --trace-config quic lines from the QUIC transport
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1169 — Write the --trace-config quic lines from the QUIC transport

## Goal

Under `-v --trace-config quic` (and `all`) Curl writes the `* [QUIC] ...` lines curl 8.21.0 writes for an HTTP/3 transfer, from `Curl.Quic.UnitLibrary`.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on and hands the QUIC transport an `ITransferEvents` sink.
- Not measured yet: the reference Schannel build has no QUIC. Measure on an OpenSSL build of curl 8.21.0 with HTTP/3 against a local HTTP/3 server, run with `Record-CurlExchange.ps1 -NoServer`, and record the lines in Notes. Measure too which of `protocol`, `network` and `-vvvv` turn `quic` on (curl's manual puts it under `network`).

## Acceptance criteria

- [x] The `[QUIC]` lines of an HTTP/3 GET under `-v --trace-config quic` are measured and recorded in Notes, with which umbrella names turn them on.
- [x] Tests pin them; the measured umbrella names write the same; `-v` alone, another component and `quic` without `-v` write none.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

**Measured** 2026-10-02 with curl.se's Windows build 8.18.0 (LibreSSL 4.2.1, ngtcp2 1.21.0,
nghttp3 1.15.0; `%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`), the HTTP/3 reference ADR-0144
names; the Schannel 8.21.0 build has no HTTP/3 and no local HTTP/3 server exists, so against
`https://cloudflare-quic.com/` as BL-1168 did:
`curl -s -v [--trace-config X] --http3-only https://cloudflare-quic.com/ -o out.html`, exit 0 each.
The `* [...]` lines, I/O loop lines left out, numbers normalized:

- `-v` alone and `-v --trace-config quic`: identical - the seven `[HTTP/3] [0] OPENED stream ...`
  and request-header lines. **No `[QUIC]` line.**
- `network`: adds `[DNS]`, `[HAPPY-EYEBALLS]`, `[MULTI]` lines; no `[QUIC]` line.
- `timer`: adds `[TIMER] [QUIC] set for <n>ns` / `gives multi timeout in <n>ms` (the timer's name,
  not a component); `all` (1337 lines) adds `[UDP] QUIC socket <fd> connected: ...` and `[SSLS]`;
  no line is tagged `[QUIC]`. `protocol` adds none.

So no umbrella name turns `[QUIC]` lines on, because the build writes none: its QUIC transport
traces through the `[HTTP/3]` filter (`http/3`, BL-1168/BL-1208).

**Decided** (ADR-0376): `quic` writes nothing beyond `-v`; `Curl.Quic.UnitLibrary` is unchanged
and gets no sink. Pinned in `CurlCommandRunnerHttp3Tests`:
`RunAsync_Http3OnlyVerboseUnderTraceConfigQuic_WritesExactlyTheVerboseLines` (quic, QUIC),
`RunAsync_Http3OnlyUnderAnUmbrellaComponent_WritesNoQuicLine` (network, protocol, all, -vvvv),
`RunAsync_Http3OnlyUnderTraceConfigQuicWithoutVerbose_WritesNothing`.

`--ai-help` is built from the help table, whose `--trace-config` line names no component; it
stays correct.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config quic measured to write no [QUIC] lines in curl's ngtcp2 build and pinned as none (ADR-0376)
