---
id: BL-3940
title: Port the -Http2 and -Http2Origin modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3939]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3940 — Port the -Http2 and -Http2Origin modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles -Http2 and -Http2Origin (with Http2ResetCode, Http2Answer, Http2NoSettings) like `Record-CurlExchange.ps1`.

## Context

Split from BL-3931 (part of BL-3934); follows BL-3929 and BL-3930 in the same file. Mode: HTTP/2 proxy and origin framing with HPACK; over TLS needs openssl s_server -alpn h2 since socat has no ALPN option; pre-built byte files are fine. Test in WSL Ubuntu (socat, curl 8.18.0, openssl present) against the .ps1 under pwsh. Platform-neutral output only.

## Acceptance criteria

- [x] The mode no longer exits 2 with "not ported yet", and the "not ported" header block no longer lists it (or states why it cannot exist off Windows).
- [x] At least one case per mode records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the .ps1; cases and results are in the Log.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

- Design: perl (core modules only) serves the HTTP/2 framing and HPACK (the 256 Huffman codes were dumped from .NET's own decoding tree and checked: 256 codes, Kraft sum 1). Plain TCP origins listen in perl itself; with TLS socat has no ALPN, so `openssl s_server -alpn` takes the port and perl serves its plaintext through FIFOs, reading the negotiated protocol from the ServerHello in s_server's msgfile.
- Not ported, stated in the header: `-Http2ResetCode` with `-Tls`, because s_server serves one connection at a time (exits 2 "not ported yet").
- Measured with real curl 8.18.0 (WSL, nghttp2) against the .ps1 under pwsh on Windows (same `--resolve` authority), request.bin, stdout.bin, stderr.txt, exitcode.txt and transcript.txt byte-identical: h2c-upgrade, h2c-upgrade-ignored, prior-knowledge, prior-knowledge + Http2ResetCode 13 (transcript lines identical but interleaved by connection in arrival order, as in the .ps1), Http2Answer push / nostatus / status:404, Http2NoSettings, Tls alpn origin (h2 and http/1.1), Tls -Http2 proxy (ProxyStatus 200 and 407, ProxyAlpn h2 and http/1.1). HTTP, HTTPS, FTP (only the random passive port differs) and SMTP cases still match.
- CI on Linux and macOS: the output is platform-neutral perl and bash; no Windows-only path or text was used.

- Design: perl (core modules only) serves the HTTP/2 framing and HPACK (the 256 Huffman codes were dumped from .NET's own decoding tree and checked: 256 codes, Kraft sum 1). Plain TCP origins listen in perl itself; with TLS socat has no ALPN, so `openssl s_server -alpn` takes the port and perl serves its plaintext through FIFOs, reading the negotiated protocol from the ServerHello in s_server's msgfile.
- Not ported, stated in the header: `-Http2ResetCode` with `-Tls`, because s_server serves one connection at a time (exits 2 "not ported yet").
- Measured with real curl 8.18.0 (WSL, nghttp2) against the .ps1 under pwsh on Windows (same `--resolve` authority), request.bin, stdout.bin, stderr.txt, exitcode.txt and transcript.txt byte-identical: h2c-upgrade, h2c-upgrade-ignored, prior-knowledge, prior-knowledge + Http2ResetCode 13 (transcript lines identical but interleaved by connection in arrival order, as in the .ps1), Http2Answer push / nostatus / status:404, Http2NoSettings, Tls alpn origin (h2 and http/1.1), Tls -Http2 proxy (ProxyStatus 200 and 407, ProxyAlpn h2 and http/1.1). HTTP, HTTPS, FTP (only the random passive port differs) and SMTP cases still match.
- CI on Linux and macOS: the output is platform-neutral perl and bash; no Windows-only path or text was used.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ebe547a6 49b8818c 541414b4.
- 2026-10-28: Doing -> Done. Record-CurlExchange.sh serves -Http2 and -Http2Origin, byte-identical to the .ps1
