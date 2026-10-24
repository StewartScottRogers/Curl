# ADR-0737 — Upstream's disabled cases stay off the ratchet; `Unicode` stays off; `ssl-sessions` is reported where `SSLS-EXPORT` is

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2858
- Decided by Claude under Stewart's delegation.

## Context

BL-2858 ran twelve upstream cases that the gap scorecard of 2026-10-19 keeps off the ratchet for a
reason other than a Curl difference: eight that curl 8.22.0's `tests/data/DISABLED` lists, two that
need the `Unicode` feature, one that needs `ssl-sessions`, and test8, which fails on Windows. Each ran
once through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` on Windows on 2026-10-21.

## Decision

1. **The eight disabled cases stay off `PassingUpstreamCases.txt`.** None passed on Windows, so none
   reached the ten-of-ten bar a disabled case must clear to be listed. `DISABLED` is not vendored in
   `CurlTestCases/Original`; the reasons below are its comments as the gap scorecard and BL-2858 record them.

   | Case | Upstream's reason for disabling it | Windows outcome |
   | --- | --- | --- |
   | 594 | fails too often to be reliable | curl did not finish within 20 seconds |
   | 836 | the test server does not behave as the case needs | differs: Curl sends `B004 UID FETCH 2 BODY[]`, the case expects `B004 FETCH 2 BODY[]` |
   | 938 | the test server does not behave as the case needs | differs: the `AUTH PLAIN` initial response is for the second user's credentials, not `user.one`'s |
   | 1182 | it overwrites the user's own `.curlrc` | skipped: the harness does not run the `mkdir`/`cp` precheck |
   | 1211 | unreliable upstream | differs: Curl sends `QUIT` where the case expects none |
   | 1307 | "fnmatch differences are too common to make testing them sensible" | skipped: no port of `unit1307` |
   | 1801 | unreliable upstream (HTTP/2) | skipped: the Schannel build Curl matches on Windows has no `http/2` |
   | 2043 | it needs `revoked.badssl.com` on the internet | skipped: the harness reaches no internet host |

   Curl's differences on 836, 938 and 1211 are real but are against cases upstream itself does not
   run; they are left to the gap office's behaviour findings rather than chased here.

2. **`Unicode` stays off on every platform.** Windows' `curl.exe` reports `Unicode`, but Curl's
   `curl -V` does not, and the harness reports only what Curl's `-V` names. Turning it on in the
   harness changed the expansion of listed test3203 (`%if Unicode` names its file
   `漢字-ä.txt`), which then failed: the listing came back with the name's UTF-8 bytes encoded a
   second time. test1703 (a UTF-8 file name in a `file://` URL) then got no output, and test472
   (`--aws-sigv4` with a non-ASCII query) signed differently. Reporting `Unicode` waits until Curl
   writes those three right; BL-2858's Notes name the task filed for it.

3. **`ssl-sessions` is reported on Linux and macOS.** `runtests.pl` derives `ssl-sessions` from
   `SSLS-EXPORT` in `curl -V`, which Curl's OpenSSL-matching build lists and its Schannel-matching
   build does not (that build refuses `--ssl-sessions`, as curl's does). `UpstreamUnixFeatures` now
   names it, so test777 runs there and is `SkippedOnThisPlatform` on Windows. test777 verifies only
   that the transfer succeeds while the session file holds lines that do not decode, which
   `TlsSessionCache.Import` reports as warnings without failing; it is listed.

4. **test8's difference was Curl's.** curl's `Curl_cookie_getlist` sends a cookie to a host that is an
   IP address only when the cookie's domain equals the address, even for a cookie that includes
   subdomains; Curl tail-matched `domain=.0.0.1` against `127.0.0.1` and sent `partialip=nono`.
   `CookieStore` now matches an IP host exactly, and test8 is listed.

## Consequences

- The ratchet holds test8 on every platform and test777 on Linux and macOS.
- The disabled eight are named here once; the gap office keeps them as `excluded | disabled-upstream`.
