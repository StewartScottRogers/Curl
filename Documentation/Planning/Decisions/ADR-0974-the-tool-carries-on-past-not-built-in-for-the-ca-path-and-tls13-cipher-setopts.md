# ADR-0974: The curl tool carries on past CURLE_NOT_BUILT_IN for the CA path and TLS 1.3 cipher setopts

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-4029, BL-4011)

## Context

`ToolEasyHandle.SetOptions` stopped at the first `curl_easy_setopt` that did not answer `CURLE_OK`
(ADR-0631). curl 8.22.0's `config2setopts` ignores `CURLE_NOT_BUILT_IN` for `CURLOPT_CAPATH`,
`CURLOPT_PROXY_CAPATH`, `CURLOPT_TLS13_CIPHERS` and `CURLOPT_PROXY_TLS13_CIPHERS`, warns and carries on.
BL-4011 makes `Libcurl` answer `CURLE_NOT_BUILT_IN` for the last three on Windows, as the Schannel build does,
so `curl --capath` and `curl --tls13-ciphers` would fail there until the tool tolerates it.

Measured with curl 8.22.0 Schannel (BL-4029): `--capath .` and `--proxy-capath .` print
`Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel` (wrapped),
`--tls13-ciphers` and `--proxy-tls13-ciphers` print `Warning: ignoring --tls13-ciphers, not supported by libcurl
with Schannel` and its proxy twin; `-s` silences them; the transfer goes on.

## Decision

`SetOptions` carries on past a `CURLE_NOT_BUILT_IN` answer to those four calls; any other answer that is not
`CURLE_OK`, and `CURLE_NOT_BUILT_IN` for any other call, still fails the transfer with
`setopt 0x<option> got bad argument`.

The warnings are not written by `SetOptions`. The run already writes exactly those measured lines before each
transfer, unless `-s`, in the Schannel build (`CurlComposition.WarningLinesBeforeEachTransfer`, ADR-0349, BL-1034),
which is the only build in which `Libcurl` answers `CURLE_NOT_BUILT_IN` for them; writing them again from the setopt
answer would print each twice. The OpenSSL builds support all four, so they neither answer it nor warn.

The static `SetOptions(setopts, setOption)` overload takes the call as a delegate, so its tests give any answer to
any option without depending on the platform's `Libcurl`.

## Consequences

BL-4011 can make `Libcurl` answer `CURLE_NOT_BUILT_IN` on Windows without failing the tool's transfers. Should the
warnings ever move from the run's lines to the setopt answers, `WarningLinesBeforeEachTransfer` must drop them in
the same change.
