---
id: BL-1411
title: Refuse URL and .netrc credentials holding control characters as curl's url.c does: exit 3 for the URL, exit 26 for .netrc, except a control character other than NUL over HTTP and WebSocket
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1402]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1411 — Refuse URL and .netrc credentials holding control characters as curl's url.c does: exit 3 for the URL, exit 26 for .netrc, except a control character other than NUL over HTTP and WebSocket

## Goal

Before connecting, a transfer whose URL user name or password percent-decodes to a control character fails as curl 8.21.0 fails it - exit 3 `error extracting credentials from URL` - and a `.netrc` entry whose login or password holds one fails with exit 26 `control code detected in .netrc credentials`; for `http`, `https`, `ws` and `wss` only `%00` in the URL is refused and `.netrc` control characters are sent.

## Context

- Today `Curl.Console/TransferCredentialLookup.cs` decodes the URL's user information with `Uri.UnescapeDataString` (`DecodedUserInformation`, around line 194) and accepts any byte, and passes `.netrc` credentials (`NetrcFile.Find`) on unchecked; neither curl text exists in the solution (`git grep "error extracting credentials"` and `git grep "control code detected"` find nothing).
- curl 8.21.0 (tag `curl-8_21_0`), `lib/url.c`:
  - lines 1557-1579: the URL's user and password are decoded with `Curl_urldecode(..., REJECT_ZERO)` when the scheme has `PROTOPT_USERPWDCTRL`, else `REJECT_CTRL`; `lib/escape.c` line 139 refuses a decoded byte below 0x20 under `REJECT_CTRL` and a decoded 0x00 under `REJECT_ZERO` (0x7f and bytes from 0x80 are accepted); a refusal gives `failf(data, "error extracting credentials from URL")` and exit 3 (`CURLE_URL_MALFORMAT`).
  - lines 1479-1489: `.netrc` credentials found for the host are refused with `failf(data, "control code detected in .netrc credentials")` and `CURLE_READ_ERROR` (exit 26) when the scheme lacks `PROTOPT_USERPWDCTRL` and the login or password holds a byte below 0x20 (`str_has_ctrl`, lines 1401-1412).
  - `lib/protocol.c` lines 135-160 and 440-465: only `http`, `https`, `ws` and `wss` carry `PROTOPT_USERPWDCTRL`.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel):
  - `curl -sSv "ftp://u%01x:p@127.0.0.1:1/f"`: stderr `* error extracting credentials from URL`, `curl: (3) error extracting credentials from URL`; exit 3 (no connect attempt). The same for `imap://u:p%0a@127.0.0.1:1/`.
  - `curl -sS "http://u%00x:p@127.0.0.1:1/"`: `curl: (3) error extracting credentials from URL`, exit 3; `http://u%01x:p@127.0.0.1:1/` is accepted (it goes on to the connect, exit 7).
  - a `.netrc` file `machine 127.0.0.1 login u password p<0x01>q` with `--netrc-file` and `ftp://127.0.0.1:PORT/f`: `* control code detected in .netrc credentials`, `curl: (26) control code detected in .netrc credentials`; exit 26, nothing sent. With `http://127.0.0.1:PORT/a` the credentials are sent: `Authorization: Basic dTpwAXE=`.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` over fake connectors pin each measured case: the two stderr lines under `-sSv`, the exit code, and that the fake connector records no connection for the refused ones.
- [ ] Tests pin that `%7f` and `%c3%a9` in an `ftp://` URL user name are accepted, `%1f` is refused, and that `ws://u%01x:p@host/` is accepted while `ws://u%00x:p@host/` is refused.
- [ ] Redirect hops keep today's behaviour for credentials they already had; a hop's own URL credentials are checked the same way (pin one `-L` case to an `ftp://` target with `%01` in its user).
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member in the code this task changed.

## Notes

- Not this task: the `-v` line `Could not find host <host> in the <file> file; using defaults` that curl writes when the netrc file has no entry for the host (`lib/url.c` lines 1466-1470); file it separately if it is still missing after this task.
- Depends on BL-1402 only because both change `Curl.Console`. No option changes, so `--ai-help` is unaffected.

## Log

- 2026-10-03: Created.
