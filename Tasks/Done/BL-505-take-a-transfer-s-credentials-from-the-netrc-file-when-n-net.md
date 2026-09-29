---
id: BL-505
title: Take a transfer's credentials from the netrc file when -n, --netrc-file or --netrc-optional asks
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-503, BL-504]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-505 — Take a transfer's credentials from the netrc file when -n, --netrc-file or --netrc-optional asks

## Goal

With `-n`, `--netrc-optional` or `--netrc-file`, `Curl.Console` looks up each URL's host in the netrc file (the default location when no file is named) and uses the login and password it finds, with curl 8.21.0's precedence against `-u` and URL user information, and curl's message and exit code when a required file is missing or broken.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). The reader is BL-503 (`Curl.Authentication.UnitLibrary`), the options BL-504.
- Credentials reach the transfer through `Curl.Console/TransferContextFactory.cs` and `HttpRequestOptionsMapping.cs` (`ITransferContext.Credentials`); every scheme that takes `-u` (HTTP, FTP, MQTT, TFTP) gets them the same way.
- Default location: `$HOME/.netrc`, and on Windows `%USERPROFILE%` with `_netrc` also tried; the exact order must be measured on each platform. Read the environment through the injected environment seam the proxy selection uses (ADR-0024), not `Environment` directly.
- Redirects to another host must not carry the first host's netrc password (compare FR-089).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `-n` with the file in the default location (both names on Windows), `-n` with no file, `--netrc-optional` with no file, `-u a:b -n`, a URL with `user@`, and an `ftp://` URL using `-Ftp`; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Console.UnitTests` tests through fake file and environment seams pin each measured case, including the missing-file message and exit code.
- [x] New tests are platform-neutral; the Windows default-location order is pinned under `[OSCondition(OperatingSystems.Windows)]` and the other in its own excluded-Windows test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-28)

Reference: Git for Windows curl 8.21.0 (x86_64-w64-mingw32, Schannel). Every case ran through
`Record-CurlExchange.ps1 -Port 18505` with `HOME` and `USERPROFILE` set on the recorder's process
to prepared directories (`.netrc` = `machine 127.0.0.1 login nu password np` unless stated). The
request is `GET / HTTP/1.1`, `Host: 127.0.0.1:18505`, [`Authorization`], `User-Agent: curl/8.21.0`,
`Accept: */*`; stderr empty and exit 0 unless stated.

| Case | Result |
| --- | --- |
| `-s -S -n`, `.netrc` in `HOME` | `Basic bnU6bnA=` (nu:np) |
| `HOME` holding `.netrc` (dot:dp) and `_netrc` (under:up) | `Basic ZG90OmRw` (dot:dp) |
| `HOME` holding only `_netrc` | `Basic dW5kZXI6dXA=` (under:up) |
| `HOME` unset, `USERPROFILE` holding `.netrc` / only `_netrc` | nu:np / under:up |
| `HOME` an empty directory, `USERPROFILE` holding `.netrc` | exit 26, `curl: (26) .netrc error: no such file` |
| `HOME` and `USERPROFILE` unset; `HOME` empty directory | exit 26, the same line |
| `-n` with no file, without `-s -S`; with `-o f` | the same line only (no progress meter); `f` not created |
| `-n` with no file, two URLs | the line twice, exit 26 |
| `--netrc-optional` with no file / with `.netrc` | no Authorization / nu:np |
| `-u a:b -n` (file there, missing, or malformed) | `Basic YTpi` (a:b), exit 0 |
| `-u q: -n` | `Basic cTo=` (q:) |
| `-u : -n`, `-u :pw -n` | nu:np |
| two entries `login a password pa` / `login b password pb`: URL `b@`, `%62@`, `b:x@` | `Basic YjpwYg==` (b:pb) each |
| the same, URL `zz@` / `zz:x@` / no user | `Basic eno6` (zz:) / `Basic eno6eA==` (zz:x) / a:pa |
| the same, `-n -u q:r` with URL `b@` | `Basic cTpy` (q:r) |
| `machine 127.0.0.1 login lo` | `Basic bG86` (lo:) |
| malformed (`login "abc`) with `-n`, also with URL `b@` / `b:x@` | exit 26, `curl: (26) .netrc error: syntax error` |
| malformed with `--netrc-optional` | no Authorization, exit 0 |
| `--netrc-file <.netrc>`; with `--netrc-optional` too | nu:np |
| `--netrc-file <a directory>` | exit 26, no such file |
| `-n file:///nonexistent` with no file | exit 26, no such file (every scheme) |
| `-n ftp://127.0.0.1:18505/f` (`-Ftp`) | `USER nu`, `PASS np`; with no file exit 26 no such file |
| `-n ftp://b@...` with the two entries | `USER b`, `PASS pb` |
| `-n ftp://...` with `login lo` only | `USER lo`, `PASS ` (empty) |
| `--netrc-optional ftp://...` with no file | `USER anonymous`, `PASS ftp@example.com` |
| `-n -L`, 302 to `/b` | both requests nu:np |
| `-n -L`, 302 to `http://localhost:18505/b`, no `localhost` entry | second request no Authorization, with or without `--location-trusted` |
| the same with `machine localhost login lu password lp` | second request `Basic bHU6bHA=` (lu:lp), with or without `--location-trusted` |
| `HOME` with a trailing backslash | nu:np |

(Setting `HOME` to `''` from PowerShell removes the variable, so the empty-`HOME` row measured an unset one.)

### What was built

- `Curl.Console/NetrcCredentialLookup.cs`: per URL, after the proxy is chosen
  (`CurlCommandRunner.TrySelectProxyAndCredentials`), decides the credentials; the runner keeps
  them as `RunningTransferState.NetrcCredentials`, and `TransferContextFactory.Create` puts them on
  every attempt's context in place of `-u`'s. File and environment come through the runner's
  `IDataFileReader` and `readEnvironmentVariable`; the runner's "no environment" function is now
  one explicitly created delegate (`NoEnvironmentVariables`) shared with the IPFS rewriter, because
  two uses of the `ReadNoEnvironmentVariable` method group shared the compiler's delegate cache and
  left one site's branch uncovered depending on test order.
- `Curl.Console.UnitTests/CurlCommandRunnerNetrcTests.cs` pins every row above except the
  per-hop lookup, over `RecordingProtocolHandler` (HTTP and FTP contexts) and, for the request
  bytes and redirects, `HttpProtocolHandler` over a `ScriptedConnector`. `--netrc-file .` is used
  because the parser checks the path on disk and `.` exists everywhere; the fake reader serves it.
- `CurlCommandRunnerUrlExpansionTests` gained a runner with no environment for an IPFS URL.
- `Curl.Console/CLAUDE.md` describes the behaviour.

### Defaults taken (rule 1)

- Every behaviour pinned was measured, so no ADR (as BL-504). The choices below are defaults.
- An empty `HOME` or `USERPROFILE` counts as unset; an empty URL user name (`http://@h/`) counts as none.
- Off Windows with no `HOME`, curl falls back to the password database; the tool finds no file
  then (`no such file`, or carries on under `--netrc-optional`). Not measurable here; `HOME` is set
  in every normal session.
- The default path is `HOME\.netrc` on Windows and `HOME/.netrc` elsewhere, as curl's `DIR_CHAR`;
  no message prints the path.
- Under `--netrc-optional` a missing or malformed file leaves the URL's own user and password in
  place, as curl keeps them on the connection.

### Left for follow-up tasks

- BL-790: curl looks the entry up again for every redirect hop's host, even under
  `--location-trusted`; the tool keeps the first hop's credentials for the same host and drops
  them for another (and carries them under `--location-trusted`). Needs `Curl.Core`'s
  `RedirectFollower`, outside this task's `touches`.
- BL-791: without a netrc option, a URL's own `user:password@` is still not sent (HTTP sends no
  Authorization, FTP logs in as anonymous).

### Gates

`dotnet build Curl.slnx -warnaserror`: clean. Fast tests: all green (Curl.Console.UnitTests 1333
passed, 4 skipped). `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch,
0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -n, --netrc-file and --netrc-optional give each transfer its netrc login and password with curl 8.21.0's precedence, messages and exit 26
