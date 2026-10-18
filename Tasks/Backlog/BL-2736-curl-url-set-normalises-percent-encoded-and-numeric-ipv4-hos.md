---
id: BL-2736
title: curl_url_set normalises percent-encoded and numeric IPv4 hosts and refuses a blank host as libcurl does (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2736 — curl_url_set normalises percent-encoded and numeric IPv4 hosts and refuses a blank host as libcurl does (lib1560 rows)

## Goal

The URL API's host handling matches libcurl 8.22.0: a percent-encoded host in a whole URL is decoded and then normalised as an IPv4 or IPv6 address, a host set alone as a part is kept as given, a blank host under `CURLU_URLENCODE` answers `CURLUE_BAD_HOSTNAME`, and `CURLU_PUNY2IDN` turns a `www.xn--` host back to Unicode, so lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.*.cs`, run by `Lib1560Checks`). The URL API goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` (`UrlPartText`) over `Curl.Protocol.Abstractions`' `CurlUrl` and `CurlUrlHost`; upstream: `lib/urlapi.c` `parse_authority`, `ipv4_normalize`, `ipv6_parse` and `curl_url_set`'s `CURLUPART_HOST`.

Rows (input, flags, wanted, got):
- `get_url_list` 0: `https://127.0.0.%31.`, `https://127.0.0.1/`, got `https://127.0.0.1./`.
- `get_url_list` 1: `https://127.0.0.0%78f%46.`, `https://127.0.0.255/`, got `https://127.0.0.0xfF./`.
- `get_url_list` 2: `https://%30%31%37%37%2e%31`, `https://127.0.0.1/`, got `https://0177.1/`.
- `get_url_list` 3: `https://[fe80%3A%3A20c%3A29ff%3Afe9c%3A409b]/`, 0, got 21.
- `get_parts_list` 10: `https://[%3A%3A13%2e1%2e68%2e3%25eth0]:1234`, host `[::13.1.68.3] eth0`, got 21.
- `set_parts_list` 7: `host=%43url.se,`, `https://%43url.se/`, got `https://Curl.se/`.
- `set_parts_list` 23: `host=0xff,`, `https://0xff/`, got `https://0.0.0.255/`.
- `set_parts_list` 39: `host=  ,` (with other blank parts) under `URLENCODE`, 21, got 0.
- `get_parts_list` 52 (runs only with `CURL_TEST_HAVE_CODESET_UTF8`): `https://www.xn--rksmrgs-5wao1o.se`, `PUNY2IDN`, host `www.räksmörgås.se`, got the punycode.

## Acceptance criteria

- [ ] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`, `SetParts`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
