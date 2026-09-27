---
id: BL-292
title: Add CurlUrl, a curl-compatible URL type and parser, to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0010-representing-urls-system-uri-cannot-round-trip.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-292 — Add CurlUrl, a curl-compatible URL type and parser, to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` has a `CurlUrl` type whose parser accepts and
rejects URLs as curl 8.21.0's URL API does, keeps the text as typed, and exposes each
part as written. Nothing uses it yet.

## Context

- ADR-0010 (Accepted, option 1 "Replace") is the design. This is its first step. The
  type is additive, so no other project changes here. BL-294 switches the contract to it.
- Model the parts on libcurl's URL API (<https://curl.se/libcurl/c/curl_url_get.html>)
  and `lib/urlapi.c` at curl 8.21.0: scheme, user, password, options, host, zone id,
  port, path, query, fragment, and the original text.
- To make BL-294 mechanical, `CurlUrl` also offers the members handlers read from
  `System.Uri` today, with the same meaning where curl agrees: `Scheme`, `Host`,
  `IdnHost` (punycode through `System.Globalization.IdnMapping`), `Port` (the scheme's
  default when none is written), `IsDefaultPort`, `AbsolutePath` (the path as written,
  dot segments removed unless parsed with path-as-is), `Query`, `Fragment` and
  `OriginalString`.
- A parse switch equivalent to `CURLU_PATH_AS_IS` keeps dot segments.
- `file:` follows curl's rules: `file://C:`, `file:///C:`, `file:///C:%2FWindows/win.ini`,
  `file:///Q:dir/../x` and `file:////server/share` parse; `file://user:pass@localhost/x`
  and `file://ab:/x` are rejected (exit 3). ADR-0010's case table has each one.
- Base class library only. Measure curl 8.21.0 (the mingw build, `/mingw64/bin/curl`,
  ADR-0018) before pinning any accept or reject outcome, and record the command and
  result under Notes.

## Acceptance criteria

- [x] `CurlUrl.TryParse(string text, bool pathAsIs, out CurlUrl? url)` exists, and a URL it rejects is reported as `CurlExitCode.UrlMalformat` (3) by the caller's contract documented on the method.
- [x] Tests in `Curl.Protocol.Abstractions.UnitTests` cover every row of ADR-0010's case table, `http://example.com/a/../b` with and without path-as-is, and `http://example.com/a%2Fb` (path stays `/a%2Fb`), each outcome measured against curl 8.21.0 and recorded under Notes.
- [x] `IdnHost`, `Port`, `IsDefaultPort`, `AbsolutePath` and `OriginalString` are tested for `http`, `https`, `dict`, `gopher`, `mqtt`, `telnet`, `tftp` and `file` URLs.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.
- `touches` gained ADR-0010: the parser decisions below are recorded in its new section
  "Parser decisions taken under BL-292". No task in `Doing` named that file.
- Plan, delivered: `CurlUrl` (public) plus internal `CurlUrlParser`, `CurlUrlScheme`,
  `CurlUrlAuthority`, `CurlUrlHost`, `CurlUrlIPv4Address` and `CurlUrlDotSegments`,
  each a port of the matching piece of `lib/urlapi.c` (`parseurl`, `Curl_is_absolute_url`,
  `guess_scheme`, `parse_authority`, `Curl_parse_login_details`, `Curl_parse_port`,
  `ipv4_normalize`, `ipv6_parse`, `hostname_check`, `dedotdotify`). The public
  `TryParse` passes `OperatingSystem.IsWindows()` as the drive-letter rule to an internal
  overload, so both platforms' rules are tested on either. `InternalsVisibleTo` added
  for the test project.
- Defaults taken: rules A and D of RFC 3986 5.2.4 (a leading `./` or `../`) are not
  ported, because a parsed path always starts with `/` or a drive letter. The dedot
  shortcut in old curl sources (skip when the path has no `.`) is not ported either:
  curl 8.21.0 turns `/a/%2e%2e/b` into `/b`.
- How curl 8.21.0 was measured (2026-09-26, `/mingw64/bin/curl`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ... libidn2/2.3.8`):
  `curl -g -s --connect-to ::127.0.0.1:9 --max-time 0.3 -o /dev/null -w
  'eff=%{url_effective} user=%{url.user} pw=%{url.password} opt=%{url.options}
  host=%{url.host} zone=%{url.zoneid} port=%{url.port} path=%{url.path}
  q=%{url.query} f=%{url.fragment}' "<url>"`, adding `--path-as-is` where the case
  needs it. Exit 3 is a reject; exit 7, 28, 37 or 0 means curl parsed it (1 for an
  unknown scheme). `%{url.path}` ignores `--path-as-is`, so path-as-is results were
  read from `%{url_effective}`. `-g` keeps the tool's globber off IPv6 brackets.
- Results pinned in `CurlUrlTests` (every row was measured unless marked):
  - ADR-0010 table: `file:///C:%2FWindows/win.ini` exit 0, path `/C:%2FWindows/win.ini`;
    `file://user:pass@localhost/x` exit 3; `file:///c|/x` path `c|/x`;
    `file:////server/share` path `//server/share`; `file:////server/../x` path `//x`;
    `file:///C:/dir\..\x` path `C:/x`, and `C:/dir/../x` with `--path-as-is`;
    `file://C:` and `file:///C:` path `C:` (exit 37); `file://ab:/x` exit 3;
    `file:///Q:dir/../x` path `/x`.
  - `http://example.com/a/../b`: path `/b`; with `--path-as-is`, `/a/../b`.
    `http://example.com/a%2Fb`: path `/a%2Fb`.
  - Default ports: http 80, https 443, dict 2628, gopher 70, mqtt 1883, mqtts 8883,
    telnet 23, tftp 69; `:080` reads as 80; an unknown scheme has none.
  - file: `localhost/`, `LOCALHOST/` and `127.0.0.1/` accepted; `file://`,
    `file://localhost`, `file://server/share/x` and `file:C:/nope` exit 3.
  - Backslashes become `/` only after `scheme://` and before `?`/`#`:
    `http://h\x/` path `/x/`; `http:/h/a\b` keeps `/a\b`; `file:/\C:/x` keeps
    `/\C:/x`; `file:/C:\x` is `C:\x`; `http:///\h/x` becomes four slashes, exit 3.
  - Hosts: the rejected characters, measured one by one with `http://a%XXb/`, are
    `!"#$%&'()*+,/:;<=>?@[\]^`{|}` plus space and controls; `-._~`, letters, digits,
    `%7F`, `%80` and `%FF` are accepted. IPv4 forms `0x7f.1`, `0X7f.1`, `2130706433`,
    `017700000001` become `127.0.0.1`; `1.2.3` becomes `1.2.0.3`; `1.2.3.4.` becomes
    `1.2.3.4`; `1.2.3.4..`, `a.b..`, `.` and `..` exit 3; `08`, `0x`, `4294967296`,
    `1.16777216` stay names. IPv6 is normalised (`[0:0:0:0:0:0:0:1]` to `[::1]`,
    `[::FFFF:1.2.3.4]` to `[::ffff:1.2.3.4]`); zone ids via `%` or `%25`, 1 to 15
    characters; `[::1%25]` exit 3.
  - Login: `u;AUTH=x:p` and `u:p;AUTH=x` split into user, password and options only for
    imap/pop3/smtp; for http `u;o` is the user and `p;o` the password.
  - Guessing: `ftp.`, `dict.`, `ldap.`, `imap.`, `smtp.`, `pop3.` (any case) name their
    scheme, anything else is http; a 40-letter scheme is read, 41 letters exit 3.
  - Not measured, from curl's source: drive letters rejected outside Windows; the
    8,000,000-byte limit; `IdnHost` punycode (`xn--exmple-jua.com`) is `IdnMapping`'s,
    because the mingw build resolved `ex%C3%A5mple.com` as `xn--exmple-qha90c.com`,
    reading the bytes in the ANSI code page (ADR-0010 records why Curl does not copy it).
- Gates: `dotnet build` 0 warnings; fast tests all green (364 in
  `Curl.Protocol.Abstractions.UnitTests`); `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Abstractions.UnitLibrary` reports 100% line, 100% branch, 346 members,
  0 failing, worst CRAP 10. `code-reviewer` found nothing to fix; its two suggestions
  (count the length limit in UTF-8 bytes; say that invalid UTF-8 hosts become U+FFFD)
  were applied.
- Follow-up already filed: BL-294 switches the contract to `CurlUrl`. Whether the Linux
  build converts backslashes as the Windows build does is not recorded; the Linux
  conformance run should check it when one exists.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. CurlUrl.TryParse parses URLs as curl 8.21.0 does, with 100% coverage; nothing uses it yet
