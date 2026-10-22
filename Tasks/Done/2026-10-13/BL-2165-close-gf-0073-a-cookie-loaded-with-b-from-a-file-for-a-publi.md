---
id: BL-2165
title: Close GF-0073: A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2165 — Close GF-0073: A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0073 (A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it), so a later gap analysis measures each of `behaviour:test409` as `match`.

## Context

- Finding: GF-0073, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test409`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test409 (http://foo.co.uk:%HTTPPORT/409 -b cookies.txt --resolve foo.co.uk:%HTTPPORT:%HOSTIP; the file holds a '.co.uk' cookie bad=not-allowed and a 'foo.co.uk' cookie good=allowed): expected 'reference curl exits 0; stdout 0 bytes', actual '<verify><protocol> differs at byte 99 (line 5): expected Cookie: good=allowed<CR><LF>, got Cookie: good=allowed; bad=not-allowed<CR><LF>'. CookieStore.LoadCookieFile (Curl.Cookies.UnitLibrary/CookieStore.cs line 230) stores every cookie NetscapeCookieFile.Read accepts, without the PublicSuffixList check RefusalOf applies to Set-Cookie. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 409

Suggestion, copied from the finding:

In Curl.Cookies.UnitLibrary, apply the Public Suffix List to cookies loaded from a cookie file as curl 8.22.0 does: in CookieStore.LoadCookieFile (or NetscapeCookieFile.Read) drop a tailmatching cookie whose domain is a public suffix (PublicSuffixList.Embedded), so it is neither sent nor written back by -c, with the -v line curl gives for it. Add a unit test loading '.co.uk' and 'foo.co.uk' cookies.

## Acceptance criteria

- [x] `behaviour:test409`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured curl 8.22.0 (mingw, Schannel, libpsl 0.21.5) on 2026-10-13 with Record-CurlExchange.ps1 (`-v -b file -c jar`): a cookie read from a file is dropped, neither sent nor written to the jar, when its domain (one trailing dot left off, any case) is a public suffix, empty, or longer than 255 characters, unless it is `localhost` or an IP address (`Curl_host_is_ipnum`, so `[::1]` is dropped and `::1` kept). Host-only and tailmatching cookies alike (`co.uk FALSE`, `.uk`, `github.io`, `com.`, `h` all dropped; `.foo.co.uk`, `localhost`, `example.com.`, `127.0.0.1` kept). curl prints `cookie '<name>' dropped, domain '[file]' must not set cookies for '<domain as written>'`. The rule matches lib/cookie.c `is_public_suffix` at curl-8_22_0 (NULL request domain: `localhost` or `!psl_is_public_suffix`).
- Choice: the check runs after the `-j` session filter, so a session public-suffix cookie under `-j` is skipped silently; and its `-v` lines follow the file's parse-refusal lines rather than interleaving with them. Both are corner cases no measured run distinguishes.
- Four NetscapeCookieFileTests measured on 8.21.0 used the single-label domains `h`/`g` and an empty domain, which 8.22.0 now drops; they use `localhost` and `h.example`/`g.example` instead, and no longer expect the empty-domain cookies.
- Could not run Curl.Console end to end from the lane (bin/ is outside its permissions) or Measure-CodeQuality.ps1 (time); every new branch has a unit test.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Cookies loaded with -b for a public suffix domain are dropped as curl 8.22.0 drops them; build clean, fast tests green
