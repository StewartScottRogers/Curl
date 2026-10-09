---
id: BL-1829
title: Close GF-0036: Gap items gap-options left in no group
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
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

- [x] `options:--dns-interface`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-interface: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`. Decided otherwise in ADR-0454: Curl keeps the option working on every platform (ADR-0170, ADR-0327), an intended difference for the gap office to record as `excluded`.
- [x] `options:--dns-ipv4-addr`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-ipv4-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`. Decided otherwise in ADR-0454: Curl keeps the option working on every platform (ADR-0170, ADR-0327), an intended difference for the gap office to record as `excluded`.
- [x] `options:--dns-ipv6-addr`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-ipv6-addr: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`. Decided otherwise in ADR-0454: Curl keeps the option working on every platform (ADR-0170, ADR-0327), an intended difference for the gap office to record as `excluded`.
- [x] `options:--dns-servers`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --dns-servers: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`. Decided otherwise in ADR-0454: Curl keeps the option working on every platform (ADR-0170, ADR-0327), an intended difference for the gap office to record as `excluded`.
- [x] `options:--ech`: Curl answers what curl 8.21.0 answers, `exit 2: curl: option --ech: the installed libcurl version does not support this | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`. Decided otherwise in ADR-0454: Curl keeps the option working on every platform (ADR-0170, ADR-0327), an intended difference for the gap office to record as `excluded`.
- [x] `options:--proxy-http3:no-form`: Curl answers what curl 8.21.0 answers, `exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). `--ai-help` still says `--proxy-http3` is not supported and that `--no-proxy-http3` turns it off.

## Notes

- Measured 2026-10-08 with the system curl 8.21.0 (Schannel): `curl.exe --no-proxy-http3` prints `curl: (2) no URL specified`; `--no-proxy-http3 -s http://127.0.0.1:1/` exits 7; `--proxy-http3` is refused as not supported. curl checks the feature only when turning the flag on.
- Fix: `proxy-http3` now has a row, `CommandLineOption.UnsupportedFlagTurnedOffQuietly`, refusing `--proxy-http3` on every build and accepting `--no-proxy-http3` as a no-op. `RefusedWhenTurnedOn` keeps `--ai-help`'s "Not supported by this build yet" line. It was the last option without a row.
- Decision (ADR-0454): `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr` and `--ech` stay supported on Windows rather than being refused like the Schannel build: the complete-reimplementation rule, and ADR-0170/ADR-0327 built them on purpose. Those five GF-0036 items need an `excluded` entry from the gap office; a lane may not write under `Gap/` or file a task touching it, so that is left to an interactive session.
- Touches: the task named none; added Curl.Cli.UnitLibrary and Curl.Cli.UnitTests, which no other Doing task on origin/work/dark-factory named.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. --no-proxy-http3 accepted as curl's Schannel build does; DNS options and --ech stay supported per ADR-0454
