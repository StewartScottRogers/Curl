---
id: BL-1440
title: Report every upstream test feature Curl has in the conformance harness and list the cases that then pass
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1439]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1440 — Report every upstream test feature Curl has in the conformance harness and list the cases that then pass

## Goal

The conformance harness reports every upstream test feature Curl actually has - so the cases that need `proxy`, `cookies`, `crypto`, `NTLM`, `Mime`, `libz`, `ipfs` and the rest run and count, instead of being skipped as "Curl lacks the feature" - and the ones that then pass are listed on the ratchet.

## Context

- `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` lists only protocol names, `SSL`, `large_file`, `local-http`, `win32` and the TLS backend; its remarks say features "Curl lacks, such as `Debug`, `cookies`, `proxy`, `libz` and `IPv6`, stay off". That is no longer true: `Curl.Console`'s own `curl -V` (2026-10-04) reports `Features: alt-svc AsynchDNS brotli ECH GSS-API HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL TLS-SRP UnixSockets zstd`, and Curl implements cookies (`Curl.Cookies.UnitLibrary`), proxies, `-F` (Mime), Digest (crypto), IPFS and `--manual`.
- The 2026-10-04 run skipped cases for: `unittest` 71, `proxy` 71, `crypto` 49, `cookies` 35, `NTLM` 27, `Mime` 24, `libz` 14, `ipfs` 11, `ftp` 8, `Debug` 6, `headers-api` 4, `HSTS` 4, `brotli` 3, `manual` 2, `Largefile` 2, `GSS-API` 2, and one each for `zstd`, `ssl-sessions`, `http/2`, `h2c`, `alt-svc`, `UnixSockets`, `Unicode`, `TrackMemory`, `PSL`, `IDN`.
- Upstream (tag `curl-8_21_0`) `tests/runtests.pl` derives the feature names from `curl -V` (e.g. `HTTP2` becomes `http/2`, and `h2c` with it) and from the build's disabled list (`cookies`, `proxy`, `Mime`, `crypto`, `ipfs`, `manual`, `headers-api`, ...); use that file as the source of each name's meaning. `unittest`, `Debug`, `TrackMemory` and `headers-api` are libcurl build or API features Curl does not have and stay off; `ftp` stays off while the harness has no FTP server (`UpstreamCaseScreening.Servers`) unless a case needs only the name.
- Depends on BL-1439, which changes the same projects and lists the cases passing before this change.

## Acceptance criteria

- [x] `UpstreamCurlPlatform` reports each feature Curl has, by upstream's spelling, with its remarks rewritten to say which features stay off and why; a test in `Curl.Conformance.UnitTests` pins the Windows and Unix lists.
- [x] A run of `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` passes; every case it reports as `passes; add N` is added to `PassingUpstreamCases.txt`; the newly runnable cases that fail stay `Inconclusive`, and the ten most common first differences among them are written in Notes for follow-up tasks.
- [x] The pass-rate table in `Curl.Conformance.UnitTests/CLAUDE.md` gains a row from that run.
- [x] `dotnet build Curl.Conformance.UnitTests -warnaserror` is clean; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no failing member.

## Notes

- Feature list decided in ADR-0420 (Decided by Claude under Stewart's delegation): every name on Curl's `curl -V` Features line by `runtests.pl`'s spelling (`http/2` + `h2c`, `http/3`), `crypto`, the tool features a build has unless disabled (`cookies`, `proxy`, `Mime`, `manual`, `DoH`, `digest`, `aws`, `netrc`, `verbose-strings`, `large-time`, `large-size`, `sha512-256`, `SSLpinning`), protocols `ws`, `wss`, `ipfs`; `xattr` off Windows only. Off: libcurl build/API features, `ftp`, names absent from `curl -V`, `codeset-utf8` (locale-dependent).
- Run 2026-10-07 (Windows): 559 listed passing (159 added), 162 failing, 1292 skipped, 721 runnable, 77.5%. The rate fell from 82.7% because many newly runnable cases fail; the passing count rose by 159. Remaining feature skips: unittest 71, Debug 8, ftp 8, headers-api 4, ssl-sessions 1, TrackMemory 1, Unicode 1.
- Ten most common first differences among the newly runnable failing cases (for follow-up tasks):
  1. 18 cases (test2059, 384, 177, 245, 175): `<verify><protocol>` Content-Length differs, e.g. expected `Content-Length: 0` got `3` - auth negotiation/rewind sends the body when curl sends none.
  2. 14 cases (test1144, 1069, 2043, 1548, 20): wrong exit code, e.g. expected 8 got 0.
  3. 12 cases (test1704, 477, 1095, 2062, 1437): no request sent at all ("expected GET ... got the end").
  4. 9 cases (test267, 67, 176, 1215, 776): NTLM type-1 message differs (flags `BoIIAA...` expected, Curl sends a different type-1 with version/domain).
  5. 6 cases (test81, 239, 170, 162, 243): same NTLM type-1 difference on `Proxy-Authorization`.
  6. 5 cases (test1071, 1030, 155, 154, 1075): the PUT after an auth challenge is never sent.
  7. 5 cases (test217, 209, 1008, 1021, 287): `-p` / proxytunnel: expected `CONNECT host:port`, Curl sends a plain GET.
  8. 4 cases (test1418, 1134, 1419, 338): connection closed where curl reuses it ("got [DISCONNECT]").
  9. 4 cases (test1475, 1273, 1043, 1040): `-C -` resume: `Range: bytes=100-` missing.
  10. 4 cases (test60, 1072, 1068, 1073): chunked upload from stdin: expected `Transfer-Encoding: chunked`, Curl sends Content-Length.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary: 0 failing members.
- Full fast suite: one Curl.Networking.UnitTests test failed once under the load of other lanes and passed on two re-runs; untouched by this task.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Conformance harness reports every upstream test feature Curl has; 159 more upstream cases pass and are on the ratchet (559)
