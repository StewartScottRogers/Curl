---
id: GF-0036
title: Gap items gap-options left in no group
area: options
platform: windows
key: options:ungrouped
severity: Low
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-11_0936
regression: false
items: [options:--dns-interface, options:--dns-ipv4-addr, options:--dns-ipv6-addr, options:--dns-servers, options:--ech, options:--proxy-http3:no-form]
touches: []
task: BL-1974
tasks: [BL-1829, BL-1974]
---
# GF-0036 - Gap items gap-options left in no group

## Summary

Gap items in options that no analyst group covered, kept so no gap is dropped.

## Evidence

- options:--dns-interface: expected exit 2: curl: option --dns-interface: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-interface x
- options:--dns-ipv4-addr: expected exit 2: curl: option --dns-ipv4-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-ipv4-addr x
- options:--dns-ipv6-addr: expected exit 2: curl: option --dns-ipv6-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-ipv6-addr x
- options:--dns-servers: expected exit 2: curl: option --dns-servers: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-servers x
- options:--ech: expected exit 2: curl: option --ech: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --ech x
- options:--proxy-http3:no-form: expected exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: option --no-proxy-http3: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information; curl --no-proxy-http3

## Suggestion

Group these items under their causes in the next gap-options report.

## Measurements

- 2026-10-08_1640: 6 of 6 items are gaps.
- 2026-10-08_2029: 6 of 6 items are gaps.
- 2026-10-10_0657: 5 of 6 items are gaps.
- 2026-10-11_0936: 0 of 6 items are gaps.
- 2026-10-11_1950: 0 of 6 items are gaps.
- 2026-10-12_0951: 0 of 6 items are gaps.
- 2026-10-12_2042: 0 of 6 items are gaps.
- 2026-10-13_1406: 0 of 6 items are gaps.
- 2026-10-14_0904: 0 of 6 items are gaps.
- 2026-10-15_0304: 0 of 6 items are gaps.
- 2026-10-15_0543: 0 of 6 items are gaps.
- 2026-10-15_0751: 0 of 6 items are gaps.
- 2026-10-15_1657: 0 of 6 items are gaps.
- 2026-10-17_2225: 0 of 6 items are gaps.
- 2026-10-19_0804: 0 of 6 items are gaps.
- 2026-10-19_1329: 0 of 6 items are gaps.
- 2026-10-22_0148: 0 of 6 items are gaps.
- 2026-10-23_1637: 0 of 6 items are gaps.
- 2026-10-25_1542: 0 of 6 items are gaps.
- 2026-10-30_2103: 0 of 6 items are gaps.

## Log

- 2026-10-08_1640: Opened for items gap-options left in no group.
- 2026-10-08_1731: Filed BL-1829.
- 2026-10-10_0756: Filed BL-1974.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
