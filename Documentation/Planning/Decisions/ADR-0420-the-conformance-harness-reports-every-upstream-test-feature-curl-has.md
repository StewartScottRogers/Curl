# ADR-0420 — The conformance harness reports every upstream test feature Curl has

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1440
- Decided by Claude under Stewart's delegation.

## Context

`UpstreamCurlPlatform` decides which upstream `<features>` and `%if` names a case may rely on.
It listed only the harness's protocols, `SSL`, the TLS backend, `large_file`, `local-http` and
`win32`, so cases needing `proxy`, `cookies`, `crypto`, `NTLM`, `Mime`, `libz`, `ipfs` and the
rest were skipped as "Curl lacks the feature" although Curl has them. Upstream's `runtests.pl`
derives the names from `curl -V` (`HTTP2` as `http/2`, with `h2c`; `HTTP3` as `http/3`), derives
`crypto` from `NTLM`, `Kerberos` and `SPNEGO`, and turns on the tool features a build has unless
its disabled list names them.

## Decision

The harness reports, on both platforms: the protocols it can serve plus `ws`, `wss` and `ipfs`;
every name on Curl's `curl -V` `Features:` line, spelled as `runtests.pl` spells it; `crypto`;
`cookies`, `proxy`, `Mime`, `manual`, `DoH`, `digest`, `aws`, `netrc`, `verbose-strings`,
`large-time`, `large-size`, `sha512-256` and `SSLpinning`; `large_file` and `local-http`.
Windows adds `win32` and `Schannel`; Linux and macOS add `OpenSSL` and `xattr` (curl writes no
extended attributes on Windows).

These stay off: libcurl's build and API features (`Debug`, `TrackMemory`, `unittest`,
`headers-api`, `form-api`, `wakeup`, `shuffle-dns`, `override-dns`, `threadsafe`, `getrlimit`,
`--libcurl`); `ftp` and other protocols the harness has no server for; names absent from Curl's
`curl -V` (`SSPI`, `Unicode`, `MultiSSL`, `ssl-sessions`, `threaded-resolver`); and
`codeset-utf8`, which depends on the test machine's locale.

## Consequences

159 more cases pass and join `PassingUpstreamCases.txt` (559 listed). 162 runnable cases fail and
stay `Inconclusive`, so the pass rate over runnable cases falls from 82.7% to 77.5% while the
passing count rises; their commonest first differences are in BL-1440's Notes. A feature Curl
gains later is added here by name.
