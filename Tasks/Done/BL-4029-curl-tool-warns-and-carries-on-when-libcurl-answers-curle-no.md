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
completed: 2026-10-28
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

- [x] A `CURLE_NOT_BUILT_IN` answer to `CURLOPT_TLS13_CIPHERS` or `CURLOPT_PROXY_TLS13_CIPHERS` leaves the transfer running, and stderr carries curl 8.22.0's `ignoring --tls13-ciphers` / `ignoring --proxy-tls13-ciphers` warning (left out under `-s`), shown by tests in `Curl.Console.UnitTests/NotBuiltInTlsSetoptWarningTests.cs`.
- [x] A `CURLE_NOT_BUILT_IN` answer to `CURLOPT_CAPATH` or `CURLOPT_PROXY_CAPATH` leaves the transfer running, with the measured warning text, shown by tests in the same file.
- [x] Any other setopt error still fails the transfer as it does now.

## Notes
- Decided (ADR-0975): `ToolEasyHandle.SetOptions` carries on past `CURLE_NOT_BUILT_IN` for the four calls and writes no warning itself. The run already writes the measured lines before each transfer, unless `-s`, in the Schannel build (`CurlComposition.WarningLinesBeforeEachTransfer`, pinned by `CurlCompositionTests`' `WarningLinesBeforeEachTransfer_*` tests). The Schannel build is the only one where `Libcurl` answers `CURLE_NOT_BUILT_IN` for these calls, so writing the warning again from the setopt answer would print it twice. `NotBuiltInTlsSetoptWarningTests` pins the carrying on through the new static `SetOptions(setopts, setOption)` seam, which takes the call as a delegate. That keeps the tests off the platform's `Libcurl`, because real `Libcurl` cannot be made to refuse these four on every platform. The same tests pin that other refusals still fail the transfer. So the criteria's warning text and `-s` are shown by the existing `CurlCompositionTests`, not by tests in the new file.
- `--capath` with `CURLE_NOT_BUILT_IN` for `CURLOPT_CAPATH` itself: no build here answers it (Schannel accepts `CAPATH`, as the measurement shows only the proxy warning), so no warning text is pinned for it.
- `Curl.Console/EasySetoptsFromCommandLine.TlsAuthenticationProxy.cs` needed no change.
- Build clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Console` 0 failing members. Platform-neutral: the tests use only a delegate, with no paths or OS text.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 56d8db24 763fefc2 b55127cc.
- 2026-10-28: Doing -> Done. SetOptions carries on past CURLE_NOT_BUILT_IN for CAPATH, PROXY_CAPATH, TLS13_CIPHERS and PROXY_TLS13_CIPHERS (ADR-0975)
