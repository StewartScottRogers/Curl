---
id: BL-2239
title: Act on <client><connectport> as runtests.pl does so case 1316 runs in the upstream case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2239 — Act on <client><connectport> as runtests.pl does so case 1316 runs in the upstream case runner

## Goal

Make the upstream case runner act on `<client><connectport>` as `runtests.pl` at curl-8_22_0 does, so case 1316 (FTP LIST through an HTTP proxy) is measured.

## Context

Split from BL-2223 (2026-10-14). `<connectport>` is not in `UpstreamCaseScreening.ClientParts`, so 1316 skips with "the harness does not act on <client><connectport>". Read what `runtests.pl` / `servers.pm` at curl-8_22_0 do with it (it names the port the proxy is expected to connect to) and check it against the proxy stand-in's recorded CONNECT target (`SwsHttpServerConnector`, BL-2035 relays FTP CONNECTs).

## Acceptance criteria

- [ ] `<client><connectport>` is acted on as in `runtests.pl`; a unit test pins it.
- [ ] test1316 is no longer screened out; added to `PassingUpstreamCases.txt` if it passes, or its failure named in Notes with a follow-up task.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; build and fast tests green; `Curl.Conformance.UnitLibrary/CLAUDE.md` updated.

## Notes

## Log

- 2026-10-14: Created (split from BL-2223).
- 2026-10-14: Backlog -> Doing.
