---
id: BL-4029
title: curl tool warns and carries on when libcurl answers CURLE_NOT_BUILT_IN for --capath, --proxy-capath, --tls13-ciphers and --proxy-tls13-ciphers
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ToolEasyHandle.cs, Curl.Console/EasySetoptsFromCommandLine.TlsAuthenticationProxy.cs, Curl.Console.UnitTests/NotBuiltInTlsSetoptWarningTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4029 — curl tool warns and carries on when libcurl answers CURLE_NOT_BUILT_IN for --capath, --proxy-capath, --tls13-ciphers and --proxy-tls13-ciphers

## Goal

When libcurl answers `CURLE_NOT_BUILT_IN` for `CURLOPT_CAPATH`, `CURLOPT_PROXY_CAPATH`, `CURLOPT_TLS13_CIPHERS` or `CURLOPT_PROXY_TLS13_CIPHERS`, Curl's tool prints curl 8.22.0's warning and carries on with the transfer, as real curl does, instead of failing the transfer with `setopt 0x... got bad argument`.

## Context

- `Curl.Console/ToolEasyHandle.cs` `SetOptions` stops at the first setopt that does not answer `CURLE_OK`. Real curl's `config2setopts` ignores `CURLE_NOT_BUILT_IN` for these four options and warns.
- Measured with curl 8.22.0 Schannel (Windows), on 2026-10-28, against `http://127.0.0.1:1/`, all exit 7:
  - `--capath .` and `--proxy-capath .`: `Warning: ignoring setting the CA path for the proxy, not supported by libcurl ` then `Warning: with Schannel` (the warning wraps over two lines).
  - `--tls13-ciphers TLS_AES_128_GCM_SHA256`: `Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel`.
  - `--proxy-tls13-ciphers ...`: `Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel`.
  - `-s` silences all of them.
- BL-4011 makes Libcurl answer `CURLE_NOT_BUILT_IN` for `PROXY_CAPATH`, `TLS13_CIPHERS` and `PROXY_TLS13_CIPHERS` on Windows. It waits for this task, because until the tool tolerates that answer, `curl --capath` (which always sets `PROXY_CAPATH` too) and `curl --tls13-ciphers` would fail on Windows.
- Test with injected setopts that libcurl refuses (the runner's `easySetoptsOf` seam, BL-2491, ADR-0631), so the test does not depend on the platform.

## Acceptance criteria

- [ ] A `CURLE_NOT_BUILT_IN` answer to `CURLOPT_TLS13_CIPHERS` or `CURLOPT_PROXY_TLS13_CIPHERS` leaves the transfer running, and stderr carries curl 8.22.0's `ignoring --tls13-ciphers` / `ignoring --proxy-tls13-ciphers` warning (left out under `-s`), shown by tests in `Curl.Console.UnitTests/NotBuiltInTlsSetoptWarningTests.cs`.
- [ ] A `CURLE_NOT_BUILT_IN` answer to `CURLOPT_CAPATH` or `CURLOPT_PROXY_CAPATH` leaves the transfer running, with the measured warning text, shown by tests in the same file.
- [ ] Any other setopt error still fails the transfer as it does now.

## Notes

## Log

- 2026-10-28: Created.
