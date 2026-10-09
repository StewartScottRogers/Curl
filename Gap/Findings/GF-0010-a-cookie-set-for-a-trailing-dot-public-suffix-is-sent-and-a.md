---
id: GF-0010
title: A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host
area: behaviour
key: behaviour:cookie-domain-matching
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1629, behaviour:test2015]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
task: BL-1803
tasks: [BL-1803]
---
# GF-0010 - A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host

## Summary

Curl differs from upstream curl in behaviour: A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host.

## Evidence

Both items expect 'upstream test<N> passes'. test1629 actual: <verify><protocol> differs at byte 156 (line 10): expected an empty line, got 'Cookie: something=1'. The reply set 'Domain=co.uk.', which the PSL must refuse, and the reference curl exits 0 and sends no cookie. test2015 (-b 'test=yes' -x ... --location to a new host): expected an empty line, got 'Cookie: test=yes'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1629,2015

## Suggestion

In Curl.Cookies.UnitLibrary: strip a trailing dot from both the Domain attribute and the request host before the PublicSuffixList check, so 'co.uk.' is refused as a public suffix. Bind a -b name=value cookie to the first URL's host, so it is not sent after a redirect to another host, as upstream test2015 expects.

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1803.
