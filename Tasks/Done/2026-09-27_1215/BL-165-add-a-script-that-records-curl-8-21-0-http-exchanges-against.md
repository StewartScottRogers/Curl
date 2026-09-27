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
completed: 2026-09-26
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

- [x] `Record-CurlExchange.ps1 -Port <n> -Response <escaped string> -CurlArgs <args> -OutDirectory <dir> [-Connections <n>] [-Curl <path>]` writes `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to `<dir>`.
- [x] Run with `-CurlArgs 'http://127.0.0.1:<port>/a?b'`, `request.bin` equals the measured default GET above with the port substituted.
- [x] It uses PowerShell and .NET `System.Net.Sockets.TcpListener` only; it runs under Windows PowerShell 5.1 and is ASCII-only.
- [x] `Curl.slnx` lists it in the `Scripts` solution folder; `dotnet build Curl.slnx` is clean.
- [x] A comment block at the top documents every parameter and one example.

## Notes

- Plan item: T1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Decided (sensible default): with `-Connections` above 1, `request.bin` holds every connection's request concatenated in accept order, so one file per fixture stays the contract. Each connection carries one request because the server closes after responding.
- Decided: a connection curl never opens is not waited for. After curl exits the server gets 2 seconds, then the listener is stopped. The script exits 0 whenever the fixtures were written, whatever curl's exit code, because failing exchanges are fixtures too.
- Decided: `-Curl` defaults to `mingw64\bin\curl.exe` in the Git for Windows install found from `git.exe` (then `%ProgramFiles%\Git`), the ADR-0009/ADR-0018 reference. `C:\Windows\System32\curl.exe` is a different build.
- Decided: `-Response` escapes are `\r \n \t \0 \\ \" \' \xHH`, decoded to Latin-1 bytes; the default is an empty 200 with `Content-Length: 0`.
- Learned: `powershell -File` hands `-CurlArgs 'a','b'` to the script as one comma-joined string; run it in-process (`&` or `.\`) to pass several arguments. Documented in the help.
- Verified 2026-09-26 on curl 8.21.0: default GET byte-exact; two `-d` POSTs over two connections with a third never opened; a chunked request body; an argument with spaces and quotes arrived intact.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Record-CurlExchange.ps1 records curl 8.21.0 request, stdout, stderr and exit code against a canned loopback server
