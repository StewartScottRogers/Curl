# ADR-0919: A `file` URL's backslashes stay as typed

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3589, CGR-060)

## Context

`CurlUrlParser` turned every `\` before the first `?` or `#` into `/` when the text after `file:` started
with `//`, on a note that curl 8.21.0 on Windows had been measured to do so. The third-party gap report's
CGR-060 found curl 8.22.0 keeps them, on Windows and Linux. Measured again on 2026-10-26 with curl 8.21.0
Schannel (`C:\windows\system32\curl.exe`) and curl 8.18.0 mingw, `curl -sS <url> -w "[%{url_effective}]"`;
both builds gave the same answer for every row:

| URL | curl (both builds) | Curl before | Curl after |
| --- | --- | --- | --- |
| `file:///c:\nonexist\zz.txt` | `(37) Could not open file c:\nonexist\zz.txt`, `[file://c:\nonexist\zz.txt]` | `c:/nonexist/zz.txt`, `[file://c:/nonexist/zz.txt]` | as curl |
| `file:///tmp/no\such\file` | `(37) Could not open file /tmp/no\such\file` | `/tmp/no/such/file` | as curl |
| `file:///C:/dir\..\x` | path `C:/dir\..\x`, with or without `--path-as-is` | `C:/x` (`C:/dir/../x` with `--path-as-is`) | as curl |
| `file://\server\share`, `file://\C:/x`, `file://\\C:/x`, `file://localhost\C:/x` | `(3) URL rejected: Bad file:// URL` | parsed | exit 3, as curl |

## Decision

A `file` URL's backslashes stay exactly as typed. They are no path separator, so dot-segment removal leaves
`dir\..` alone, and after `file://` a backslash begins the authority, which curl then rejects as not empty,
`localhost` or `127.0.0.1`. On Windows the operating system still reads `\` as a separator when the file is
opened, as it does for curl.

`CurlUrlParser.Parse` has no parameter for keeping a `file` URL's backslashes: they always stay (BL-3865).

## Consequences

`ConvertBackslashesAfterDoubleSlash` is gone, and the tests that pinned the old conversion
(`CurlUrlTests`, `CurlUrlFileUrlApiTests`, `FileUrlPathTests`) now pin the measured answers.
`Cgr060Tests` holds the issue's repros.
