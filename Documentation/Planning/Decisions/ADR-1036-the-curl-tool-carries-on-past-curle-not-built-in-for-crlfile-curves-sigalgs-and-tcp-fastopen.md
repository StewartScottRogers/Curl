# ADR-1036: The curl tool carries on past CURLE_NOT_BUILT_IN for CRL file, curves, signature algorithms and TCP Fast Open

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4182, AF-0461)

## Context

ADR-0995 makes `Libcurl` answer `CURLE_NOT_BUILT_IN` on Windows, as curl 8.22.0's Schannel build does, for
`CURLOPT_CRLFILE`, `CURLOPT_PROXY_CRLFILE`, `CURLOPT_SSL_EC_CURVES`, `CURLOPT_SSL_SIGNATURE_ALGORITHMS` and
`CURLOPT_TCP_FASTOPEN`. ADR-0975 lets `ToolEasyHandle.SetOptions` carry on past that answer only for the CA path
and TLS 1.3 cipher setopts, so `--crlfile`, `--proxy-crlfile`, `--curves`, `--sigalgs` and `--tcp-fastopen`
failed on Windows with exit 4 and `setopt 0x... got bad argument`, sending nothing.

Measured by the conformance auditor (AF-0461) with curl 8.22.0 (x86_64-w64-mingw32, Schannel) against a
loopback server: each of the five options exits 0, prints no `curl:` or warning line and sends the GET.

## Decision

`ToolEasyHandle.SetOptions` carries on past a `CURLE_NOT_BUILT_IN` answer to these five setopts too, silently:
unlike the CA path and TLS 1.3 cipher options, curl prints no warning for them. Any other refusal of them still
fails the transfer, as does `CURLE_NOT_BUILT_IN` for any setopt outside the list.

## Consequences

- On Windows the five options are accepted and ignored, as with the Schannel build; on Linux and macOS
  `Libcurl` accepts them, so nothing changes there.
- The list of tolerated setopts lives in one place, `ToolEasyHandle.IgnoredWhenNotBuiltIn`.
