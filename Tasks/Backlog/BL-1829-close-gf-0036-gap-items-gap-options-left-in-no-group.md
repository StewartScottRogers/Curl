---
id: BL-1829
title: Close GF-0036: Gap items gap-options left in no group
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-08
completed:
---
# BL-1829 — Close GF-0036: Gap items gap-options left in no group

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0036 (Gap items gap-options left in no group), so a later gap analysis measures each of `options:--dns-interface`, `options:--dns-ipv4-addr`, `options:--dns-ipv6-addr`, `options:--dns-servers`, `options:--ech`, `options:--proxy-http3:no-form` as `match`.

## Context

- Finding: GF-0036, filed by the gap analysis office (ADR-0433).
- Area: options. Severity: Low. Introduced in: not stated upstream.
- Items: `options:--dns-interface`, `options:--dns-ipv4-addr`, `options:--dns-ipv6-addr`, `options:--dns-servers`, `options:--ech`, `options:--proxy-http3:no-form`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/*.md` in the curl 8.21.0 release tarball.
- Touches: none. The finding named no Curl project outside the office, so this task has no touches and runs alone.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

- options:--dns-interface: expected exit 2: curl: option --dns-interface: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-interface x

- options:--dns-ipv4-addr: expected exit 2: curl: option --dns-ipv4-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-ipv4-addr x

- options:--dns-ipv6-addr: expected exit 2: curl: option --dns-ipv6-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-ipv6-addr x

- options:--dns-servers: expected exit 2: curl: option --dns-servers: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --dns-servers x

- options:--ech: expected exit 2: curl: option --ech: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information; curl --ech x

- options:--proxy-http3:no-form: expected exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information, actual exit 2: curl: option --no-proxy-http3: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information; curl --no-proxy-http3

Suggestion, copied from the finding:

Group these items under their causes in the next gap-options report.

## Acceptance criteria

- [ ] `options:--dns-interface`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-interface: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--dns-ipv4-addr`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-ipv4-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--dns-ipv6-addr`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-ipv6-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--dns-servers`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-servers: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--ech`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --ech: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--proxy-http3:no-form`: Curl answers what curl 8.21.0 answers, `exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
