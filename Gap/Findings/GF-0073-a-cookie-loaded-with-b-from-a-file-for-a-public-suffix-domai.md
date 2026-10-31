---
id: GF-0073
title: A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it
area: behaviour
platform: windows
key: behaviour:cookie-file-public-suffix-domain-not-dropped
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test409]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
task: BL-2165
tasks: [BL-2165]
---
# GF-0073 - A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it

## Summary

Curl differs from upstream curl in behaviour: A cookie loaded with -b from a file for a public suffix domain (.co.uk) is sent, where curl drops it.

## Evidence

behaviour:test409 (http://foo.co.uk:%HTTPPORT/409 -b cookies.txt --resolve foo.co.uk:%HTTPPORT:%HOSTIP; the file holds a '.co.uk' cookie bad=not-allowed and a 'foo.co.uk' cookie good=allowed): expected 'reference curl exits 0; stdout 0 bytes', actual '<verify><protocol> differs at byte 99 (line 5): expected Cookie: good=allowed<CR><LF>, got Cookie: good=allowed; bad=not-allowed<CR><LF>'. CookieStore.LoadCookieFile (Curl.Cookies.UnitLibrary/CookieStore.cs line 230) stores every cookie NetscapeCookieFile.Read accepts, without the PublicSuffixList check RefusalOf applies to Set-Cookie. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 409

## Suggestion

In Curl.Cookies.UnitLibrary, apply the Public Suffix List to cookies loaded from a cookie file as curl 8.22.0 does: in CookieStore.LoadCookieFile (or NetscapeCookieFile.Read) drop a tailmatching cookie whose domain is a public suffix (PublicSuffixList.Embedded), so it is neither sent nor written back by -c, with the -v line curl gives for it. Add a unit test loading '.co.uk' and 'foo.co.uk' cookies.

## Measurements

- 2026-10-13_1406: 1 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2165.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
