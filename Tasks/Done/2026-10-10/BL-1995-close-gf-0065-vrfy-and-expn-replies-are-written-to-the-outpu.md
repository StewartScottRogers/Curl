---
id: BL-1995
title: Close GF-0065: VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1995 — Close GF-0065: VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0065 (VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF), so a later gap analysis measures each of `behaviour:test924`, `behaviour:test925`, `behaviour:test927`, `behaviour:test950` as `match`.

## Context

- Finding: GF-0065, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test924`, `behaviour:test925`, `behaviour:test927`, `behaviour:test950`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test924 (--mail-rcpt smith, <data crlf="yes">): 'the --output file against <reply><data> differs at byte 33 (line 1): expected "553-Ambiguous; Possibilities are:\r\n", got "553-Ambiguous; Possibilities are:\n"'. test925 (252 reply), 927 (-X EXPN) and 950 (--request VRFY) are the same. ADR-0135 point 4 says each line goes out 'with its line end as it arrives'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 924,925,927,950

Suggestion, copied from the finding:

Find which side drops the CR. If Curl.Protocol.Smtp.UnitLibrary's SmtpCommandTransfer writes the reply lines with LF, write each with the CR LF it arrived with. If Curl.Conformance.UnitLibrary's SmtpResponder sends a crlf="yes" <data> part with bare LF, send it with CR LF, as upstream's ftpserver.pl does. Pin it in the matching .UnitTests. Explained by ADR-0135: line ends are passed through as they arrive, so the gap stands until the bytes match.

## Acceptance criteria

- [x] `behaviour:test924`: Curl answers what curl 8.21.0 answers, `upstream test924 passes`, so the item measures `match`.
- [x] `behaviour:test925`: Curl answers what curl 8.21.0 answers, `upstream test925 passes`, so the item measures `match`.
- [x] `behaviour:test927`: Curl answers what curl 8.21.0 answers, `upstream test927 passes`, so the item measures `match`.
- [x] `behaviour:test950`: Curl answers what curl 8.21.0 answers, `upstream test950 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: the conformance harness, not Curl. `SmtpServerConnector` handed `SmtpResponder` the raw `<reply>` parts, so a `crlf="yes"` `<data>` went out with bare LF, and Curl (which passes reply line ends through as they arrive, ADR-0135) wrote LF. It now serves each part through `UpstreamTestPartBodies.Served`, as `Pop3ServerConnector` and `ImapServerConnector` already do. Curl.Protocol.Smtp.UnitLibrary needed no change.
- Pinned by `SmtpServerConnectorTests.ConnectAsync_VrfyWithCrlfReplyData_SendsTheReplyLinesWithCrlf`; 924, 925, 927 and 950 now pass the ratchet and are listed in `PassingUpstreamCases.txt`.
- No option changed, so `--ai-help` is untouched. Coverage not re-measured: a one-line lambda-to-method-group change on an already covered line.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. SMTP stand-in serves crlf=yes reply data with CR LF, so upstream tests 924, 925, 927 and 950 pass
