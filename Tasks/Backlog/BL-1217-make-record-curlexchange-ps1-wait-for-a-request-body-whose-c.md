---
id: BL-1217
title: Make Record-CurlExchange.ps1 wait for a request body whose Content-Length header ends in CRLF
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-02
completed:
---
# BL-1217 — Make Record-CurlExchange.ps1 wait for a request body whose Content-Length header ends in CRLF

## Goal

Record-CurlExchange.ps1 reads a request's whole body before answering when the headers carry `Content-Length`, as its help says, so request.bin holds the body of a request whose head and body arrive in separate reads.

## Context

- Found in BL-1215: `Test-RequestComplete` matches `(?im)^Content-Length:[ \t]*(\d+)[ \t]*$`, but in .NET's multiline mode `$` matches only before `\n`, and every header line ends `\r\n`, so the `\r` defeats the match and the request counts as complete as soon as the header block has arrived. Real curl `-d @<100000 bytes>` is recorded as 65536 bytes (its first write), and the server then closes with the rest unread, which resets the connection.
- Fix: allow an optional `\r` before the anchor (`[ \t]*\r?$`).

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi' -CurlArgs '-sS,-d,@<file of 100000 bytes>,http://127.0.0.1:<port>/'` with real curl writes a request.bin of 100153 bytes (the head and the whole body).
- [ ] A `-d ab` recording still writes the head and `ab`, and curl exits 0.

## Notes

## Log

- 2026-10-02: Created.
