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
completed: 2026-10-14
---
# BL-2239 — Act on <client><connectport> as runtests.pl does so case 1316 runs in the upstream case runner

## Goal

Make the upstream case runner act on `<client><connectport>` as `runtests.pl` at curl-8_22_0 does, so case 1316 (FTP LIST through an HTTP proxy) is measured.

## Context

Split from BL-2223 (2026-10-14). `<connectport>` is not in `UpstreamCaseScreening.ClientParts`, so 1316 skips with "the harness does not act on <client><connectport>". Read what `runtests.pl` / `servers.pm` at curl-8_22_0 do with it (it names the port the proxy is expected to connect to) and check it against the proxy stand-in's recorded CONNECT target (`SwsHttpServerConnector`, BL-2035 relays FTP CONNECTs).

## Acceptance criteria

- [x] `<client><connectport>` is acted on as in `runtests.pl`; a unit test pins it.
- [x] test1316 is no longer screened out; added to `PassingUpstreamCases.txt` if it passes, or its failure named in Notes with a follow-up task.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; build and fast tests green; `Curl.Conformance.UnitLibrary/CLAUDE.md` updated.

## Notes

- Measured upstream (2026-10-14): `runtests.pl`, `runner.pm` and `servers.pm` at curl-8_22_0 never read `<client><connectport>`; it is a leftover part. `sws.c` in proxy mode (`--connect`) parses the port from the `CONNECT host:port` request line (`req->connect_port`) and `http_connect` tunnels to `connecthost:connect_port`. The proxy stand-in already does the same (it relays a CONNECT to `%FTPPORT` and the passive port, BL-2035), so "acting on it as runtests.pl does" is accepting and ignoring it, as `<client><stdout>` is (BL-2221). Sensible default, a harness fidelity fix rather than a product behaviour choice, so no ADR.
- `connectport` joined `UpstreamCaseScreening.ClientParts`; `UpstreamCaseScreeningTests.FindSkipReason_ClientConnectPort_IsAcceptedAsRuntestsPlIgnoresIt` pins it on test1316. test1316 passes and is listed. Quality run: 0 failing members.

## Log

- 2026-10-14: Created (split from BL-2223).
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. <client><connectport> is accepted and ignored as runtests.pl does; test1316 passes and is on the ratchet
