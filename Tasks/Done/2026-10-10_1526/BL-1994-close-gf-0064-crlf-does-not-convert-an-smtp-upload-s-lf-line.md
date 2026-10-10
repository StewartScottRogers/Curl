---
id: BL-1994
title: Close GF-0064: --crlf does not convert an SMTP upload's LF line ends to CR LF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1994 — Close GF-0064: --crlf does not convert an SMTP upload's LF line ends to CR LF

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0064 (--crlf does not convert an SMTP upload's LF line ends to CR LF), so a later gap analysis measures each of `behaviour:test941` as `match`.

## Context

- Finding: GF-0064, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test941`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test941 (smtp -T upload --crlf) expected 'upstream test941 passes', actual '<verify><upload> differs at byte 15 (line 1): expected "From: different\r\n", got "From: different\n"'. Curl.Protocol.Smtp.UnitLibrary and the mail options have no --crlf setting. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 941

Suggestion, copied from the finding:

Carry --crlf into MailRequestOptions (Curl.Console's MailRequestOptionsMapping, Curl.Protocol.Abstractions.UnitLibrary). In Curl.Protocol.Smtp.UnitLibrary, convert each bare LF of the upload to CR LF before dot-stuffing (SmtpDotStuffer), as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test941`: Curl answers what curl 8.21.0 answers, `upstream test941 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- `--crlf` already reached every handler as `ITransferContext.ConvertLineEndings` (TransferContextFactory), so no Curl.Console or MailRequestOptions change was needed; only SMTP ignored it.
- `SmtpDotStuffer` now takes `convertsLineFeeds`: under `--crlf` it inserts a CR before each LF not already after a CR, then dot-stuffs the result, so a line made by the conversion is stuffed too (`a\n.b` -> `a\r\n..b`). The CR-before state carries across reads, as curl's `cr_lc` reader and the FTP and file converters do. Conversion before stuffing follows the finding's evidence (test941 expects `From: different\r\n`) and curl's reader order (the LF->CRLF content reader sits below SMTP's end-of-body reader).
- `%{size_upload}` and the progress count are the converted, stuffed bytes, as for FTP and file (ADR-0003); the progress total stays the file's length.
- Upstream test941 could not be rerun here: lanes may not read `Gap/` or the gap cache (audit guard); the next gap run re-measures it.
- `--ai-help`: no option added or changed; its `--crlf` text ("convert LF to CRLF in an upload") stays true.
- Tests: `SmtpDotStufferTests.Encode_ConvertingLineFeeds_InsertsCrBeforeEachBareLfAcrossChunks`, `SmtpProtocolHandlerUploadTests.ExecuteAsync_UploadWithCrlf_SendsEachBareLineFeedAsCrlf`; both branches of the new condition are reached by the fast tests.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. SMTP -T --crlf now sends each bare LF as CRLF before dot-stuffing, as curl 8.21.0 does in test941
