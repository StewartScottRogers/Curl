---
id: BL-5336
title: Report a lone 1xx head's code and size when the connection closes before a final head
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5336 — Report a lone 1xx head's code and size when the connection closes before a final head

## Goal

When a server sends only a 1xx head and then closes the connection, `-w` reports that head's code and size (`%{http_code}` 100, `%{size_header}` 25) while the transfer still fails with exit 52, as curl does.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-086 (Low, Confirmed, 2 runs each for 100 and 103). Area: HTTP/1.1 interim responses and write-out, Windows.

**Repro.** A raw server on 127.0.0.1:31000 sends exactly `HTTP/1.1 100 Continue\r\n\r\n` and closes. `curl -sS -i http://127.0.0.1:31000/ -w "[%{http_code} %{response_code} %{size_header}]"`. Variant: `HTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n`.

**Expected (REF).** `[100 100 25]` (103: `[103 103 34]`) and `curl: (52) Empty reply from server`, exit 52.

**Actual (PORT).** Same head output and stderr, exit 52, but `[000 000 0]`.

**Where to start.** The HTTP/1 response-head loop in `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` drops the 1xx code and header bytes when the connection closes before a final head.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests` (a new file) prove that, over a stand-in connection delivering `HTTP/1.1 100 Continue\r\n\r\n` then close, the transfer fails with the empty-reply error (`CurlExitCode` 52) and its transfer info records response code 100 and header size 25.
- [ ] A test proves the same for `HTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n` (code 103, header size 34).
- [ ] A test proves a close with no bytes at all still reports code 0 and size 0.
- [ ] Tests are platform-neutral (stand-in connection, no socket).
- [ ] Hand check recorded in the Log: the repro prints `[100 100 25]` and exits 52. Where possible compare with real curl through `Record-CurlExchange.ps1` (extend it if it cannot send a bare 1xx and close); a raw server is a PowerShell or C# file-based app, never Python.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `[100 100 25]` and exits 52.

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 9295fee4 f48e2ccd d6cb3883 1002a110 43e1d4a0 a0b49a29.
