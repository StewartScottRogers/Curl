---
id: BL-2125
title: Upgrade Git for Windows on the factory machine so the reference curl is 8.22.0
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Record-CurlExchange.ps1]
lane: no
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2125 — Upgrade Git for Windows on the factory machine so the reference curl is 8.22.0

## Goal

The factory machine's Git for Windows is 2.56.0 or later, so the matched Windows reference
build that `Record-CurlExchange.ps1` and the gap runs find is curl 8.22.0.

## Context

ADR-0479 step 3.1 (retarget to curl 8.22.0). On 2026-10-13 the machine had Git 2.55.0.windows.3,
whose `C:\Program Files\Git\mingw64\bin\curl.exe -V` says `curl 8.21.0 (x86_64-w64-mingw32)
libcurl/8.21.0 Schannel ...`. Git for Windows 2.56.0.windows.1 (2026-09-28) and later "Comes
with cURL v8.22.0"; the latest on 2026-10-13 was 2.56.0.windows.2. The `windows-latest` CI
image already has 2.56.0.

Interactive only: it installs software on the machine the lanes run on, and no lane should be
mid-measurement when the reference changes. Upgrade between shifts (for example
`winget upgrade --id Git.Git -e`), or ask Stewart to run the installer if it needs his
elevation. Nothing in the repository changes unless `Record-CurlExchange.ps1`'s lookup fails
on the new layout.

## Acceptance criteria

- [x] `& 'C:\Program Files\Git\mingw64\bin\curl.exe' -V` on the factory machine prints a first line starting `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, recorded in this task's Notes with its full output.
- [x] `Record-CurlExchange.ps1` still finds that curl and records an exchange with it (a plain GET whose request shows `User-Agent: curl/8.22.0`), recorded in Notes.

## Notes

- 2026-10-13 (interactive): Stewart installed Git for Windows 2.56.0.windows.2 from the GitHub release. winget offered only 2.55.0.5, and its install failed while Git Bash processes ran. Git 2.56.0 moved curl from `mingw64\bin` to `ucrt64\bin`, so the first criterion's path is now `C:\Program Files\Git\ucrt64\bin\curl.exe`. `-V` gives:

```
curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP
Release-Date: 2026-09-02
Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss
Features: alt-svc AsynchDNS brotli Debug HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
WARNING: this libcurl is Debug-enabled, do not use in production
```

  The build is **debug-enabled**. `-V` prints a `WARNING: this libcurl is Debug-enabled` line first and lists the `Debug` feature; normal transfers print no extra stderr. BL-2128 must strip that line from the reference's `-V` and keep `Debug` excluded as debug-build-only.
- `Record-CurlExchange.ps1` and the gap office's `Get-GapReferenceCurlPath` looked only in mingw64, so they could not find the reference. PR #132 makes them try ucrt64 first. With it, `-Port 18081 -Response 'HTTP/1.1 200 OK...' -CurlArgs 'http://127.0.0.1:18081/a?b'` recorded `GET /a?b HTTP/1.1`, `Host: 127.0.0.1:18081`, `User-Agent: curl/8.22.0`, `Accept: */*`, exit 0. The audit's `Invoke-DifferentialConformance.ps1` gets the same fix on the audit branch (with BL-2132).

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing. Git for Windows 2.56.0.windows.2 installed: reference curl 8.22.0 in ucrt64\bin (PR #132 finds it)
- 2026-10-13: Doing -> Done. Git for Windows 2.56.0.windows.2 installed: reference curl 8.22.0 in ucrt64\bin (PR #132 finds it)
