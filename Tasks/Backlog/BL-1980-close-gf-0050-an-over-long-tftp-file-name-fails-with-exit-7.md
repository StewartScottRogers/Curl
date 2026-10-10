---
id: BL-1980
title: Close GF-0050: An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1980 — Close GF-0050: An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0050 (An over-long TFTP file name fails with exit 7 when the channel cannot open, where curl refuses it first with exit 71), so a later gap analysis measures each of `behaviour:test1453` as `match`.

## Context

- Finding: GF-0050, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1453`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1453 (tftp://%HOSTIP:%NOLISTENPORT/ with a 504-character name) expected 'upstream test1453 passes', actual '<verify><errorcode>: expected exit code 71, got 7'. Cause: Curl.Protocol.Tftp.UnitLibrary's TftpProtocolHandler opens the datagram channel (line 152) before TftpRequestFile.TryBuildRequest checks the length, so a channel that fails to open wins. curl's UDP socket opens without contacting the peer, and tftp_send_first refuses the name with exit 71. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1453

Suggestion, copied from the finding:

In Curl.Protocol.Tftp.UnitLibrary's TftpProtocolHandler, run TftpRequestFile's length checks ('TFTP filename too long', 'TFTP buffer too small for options', exit 71) before opening the datagram channel, as the 'Missing filename' check already is. Pin it with a test whose datagram connector refuses to open.

## Acceptance criteria

- [ ] `behaviour:test1453`: Curl answers what curl 8.21.0 answers, `upstream test1453 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
