---
id: BL-165
title: Add a script that records curl 8.21.0 HTTP exchanges against a loopback server as test fixtures
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-26
completed:
---
# BL-165 — Add a script that records curl 8.21.0 HTTP exchanges against a loopback server as test fixtures

## Goal

`Record-CurlExchange.ps1` at the repository root, listed in the `Scripts` solution folder of `Curl.slnx`, serves a canned response on loopback, runs real curl with given arguments, and saves the request bytes, stdout, stderr and exit code as fixture files.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item T1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Expected bytes in HTTP tests must come from real curl measured first. The plan's measurements were taken with a throwaway Python server; port its behaviour, not its language (no Python dependency):
- It binds `127.0.0.1:<port>`, takes the canned response as a string with `\r\n`-style escapes decoded to Latin-1 bytes, accepts N connections (default 1), reads each request until the header block ends and, when `Content-Length` or `chunked` is present, until the body has arrived (1-second read timeout), records the raw request bytes, sends the canned response and closes.
- Example it produced (default GET): `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- curl 8.21.0 is installed locally as `/mingw64/bin/curl` (the reference, ADR-0009).

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Port <n> -Response <escaped string> -CurlArgs <args> -OutDirectory <dir> [-Connections <n>] [-Curl <path>]` writes `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to `<dir>`.
- [ ] Run with `-CurlArgs 'http://127.0.0.1:<port>/a?b'`, `request.bin` equals the measured default GET above with the port substituted.
- [ ] It uses PowerShell and .NET `System.Net.Sockets.TcpListener` only; it runs under Windows PowerShell 5.1 and is ASCII-only.
- [ ] `Curl.slnx` lists it in the `Scripts` solution folder; `dotnet build Curl.slnx` is clean.
- [ ] A comment block at the top documents every parameter and one example.

## Notes

- Plan item: T1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
