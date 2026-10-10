---
id: BL-1974
title: Re-close GF-0036: Gap items gap-options left in no group
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1974 — Re-close GF-0036: Gap items gap-options left in no group

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0036 (Gap items gap-options left in no group), so a later gap analysis measures each of `options:--dns-interface`, `options:--dns-ipv4-addr`, `options:--dns-ipv6-addr`, `options:--dns-servers`, `options:--ech`, `options:--proxy-http3:no-form` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0036 ([BL-1829]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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
- [x] `options:--proxy-http3:no-form`: Curl answers what curl 8.21.0 answers, `exit 2: curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (interactive): touches set to Curl.Cli.UnitLibrary and Curl.Cli.UnitTests (where BL-1829 fixed GF-0036) so it no longer runs alone and holds other tasks back.
- 2026-10-10 (lane 5): Measured this tree: `curl --no-proxy-http3` prints `curl: (2) no URL specified` and exits 2, as curl 8.21.0 does. The BL-1829 fix (commit 691001c20, 2026-10-08) holds and is pinned in `CommandLineSchannelBuildRefusalTests`; the gap run that reopened GF-0036 must have measured a ref older than that fix. No code change needed.
- 2026-10-10 (lane 5): The five unticked items (`--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr`, `--ech`) are intended differences under ADR-0454 (Decided by Claude under Stewart's delegation: the complete-reimplementation rule keeps them supported rather than copying the Schannel build's missing c-ares and ECH). They close only through an `excluded` entry with that reason in the gap office's baseline, which a lane may neither write nor file a task for. Left to an interactive session.
- 2026-10-10 (lane 5): Build clean; Curl.Cli.UnitTests 3889 passed, 0 failed, 17 skipped. No option changed, so `--ai-help` needs nothing.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Blocked. Interactive session: --no-proxy-http3 already matches; the five DNS/--ech items are intended differences (ADR-0454) and close only by an excluded entry in the gap office baseline, which lanes may not write
