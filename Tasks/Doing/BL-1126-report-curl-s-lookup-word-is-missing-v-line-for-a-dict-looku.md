---
id: BL-1126
title: Report curl's lookup word is missing -v line for a dict lookup without a word
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1125]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1126 — Report curl's lookup word is missing -v line for a dict lookup without a word

## Goal

Under `-v`, a `dict://` `MATCH` or `DEFINE` URL whose word is empty or missing reports curl 8.21.0's `lookup word is missing` info line before the request is sent, as curl does; the bytes sent stay as they are (`default` is still the word).

## Context

- curl 8.21.0, `lib/dict.c` `dict_do` (https://github.com/curl/curl/blob/curl-8_21_0/lib/dict.c): in both the `/m:` and `/d:` branches, `if(!word || (*word == (char)0)) infof(data, "lookup word is missing");` before `unescape_word("default")` and the send.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -CurlArgs "-v,dict://127.0.0.1:<port>/d::db"`: stderr has `* lookup word is missing` right before `} [48 bytes data]`, and the request was `CLIENT libcurl 8.21.0\r\nDEFINE db default\r\nQUIT\r\n` (exit 0).
- Curl today: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs` `Word(fields)` silently substitutes `DefaultWord`; `DictProtocolHandler.ExchangeAsync` reports only the data sent. Nothing reports the line. `DictRequest.TryEncode` is a pure encoder, so the clean shape is for it (or a sibling member) to say whether the word was missing and for `ExchangeAsync` to call `context.Events.ReportInfo` before `ReportDataSent`.
- Paths that are not `MATCH`/`DEFINE` (a plain command such as `/help`) report nothing, as curl's third branch has no such line.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Dict.UnitTests` (for example in `DictProtocolHandlerTransferEventsTests.cs`) pin, for `/d:`, `/d::db`, `/m:`, `/m::db:strat`, `/find:` and `/lookup:`, the info line `lookup word is missing` reported before the request's data-sent event.
- [ ] Tests pin that `/d:word`, `/m:word` and `/help` report no such line.
- [ ] The bytes sent for every existing `DictProtocolHandlerTests` case are unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Dict.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
