---
id: BL-653
title: Write the --libcurl setopt lines for the HTTP, output and connection options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-652]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-653 — Write the --libcurl setopt lines for the HTTP, output and connection options

## Goal

The `--libcurl` generator writes curl 8.21.0's `curl_easy_setopt` lines (and any `curl_slist`/`curl_mime` set-up and cleanup) for the HTTP options (`-X`, `-H`, `-d` family, `-F`, `-u`, `-L`, `-e`, `-A`, `-b`, `-c`, `--compressed`, `-I`, `-f`), output options (`-o`, `-O`, `-i`) and connection options (timeouts, `--resolve`, `--connect-to`, `-4`/`-6`), in curl's order and formatting.

## Context

- Conformance audit 2026-09-28, row 30. Builds on BL-652.
- Measure each option's lines with `Record-CurlExchange.ps1` (`--libcurl -` with one option at a time, then a combined command line to confirm ordering); copy the text into Notes.

## Acceptance criteria

- [x] Measured first as above.
- [x] `Curl.Cli.UnitTests` reproduce each measured output byte for byte (data rows), including the combined case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-10-01 against curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -CurlArgs --libcurl,-,-s,...` (port 47653, empty 200, or `-NoServer`; the source is written whether the transfer succeeds or not). Every case starts with `BUFFERSIZE`, `URL` and `NOPROGRESS` and ends with the skeleton (ADR-0306). The lines each option adds, and where they go:

- `-X PUT`: `CURLOPT_CUSTOMREQUEST, "PUT"` after `SSLVERSION`.
- `-d a=1`: `CURLOPT_POSTFIELDS, "a=1"` and `CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)3` before `USERAGENT`. `-d a=1 -d b=2` gives `"a=1&b=2"`/7. `--data-raw x@y`, `--data-binary @up.txt` (`"hello\n"`/6), `-d @up.txt` (`"hello"`/5) and `--data-urlencode "a b"` (`"a+b"`) behave the same. `-d ''` writes `POSTFIELDS ""` and no size. `-G -d a=1` writes no `POSTFIELDS`; the URL becomes `".../f.txt\?a=1"` (left to BL-654).
- `-H 'X-A: 1' -H 'X-B: 2'`: `struct curl_slist *slist1;`, `slist1 = NULL;` and `slist1 = curl_slist_append(slist1, "X-A: 1");` per header before `curl_easy_init`, then `CURLOPT_HTTPHEADER, slist1` after the body, and `curl_slist_free_all(slist1); slist1 = NULL;` after `curl = NULL;`.
- `--json {}`: the body, plus `Content-Type: application/json` and `Accept: application/json` appended to the header list. `-H 'X: 1' --json {} -H 'Accept: x'` appends only `Content-Type`.
- `-F a=1`: `curl_mime *mime1; curl_mimepart *part1;`, `mime1 = NULL;`, then in the body `mime1 = curl_mime_init(curl);`, `part1 = curl_mime_addpart(mime1);`, `curl_mime_data(part1, "1", CURL_ZERO_TERMINATED);`, `curl_mime_name(part1, "a");` and `CURLOPT_MIMEPOST, mime1` where `POSTFIELDS` would be. It is freed with `curl_mime_free(mime1); mime1 = NULL;`. `@file` gives `curl_mime_filedata`. `<file` gives filedata plus `curl_mime_filename(part1, NULL)` and drops `;filename=`. `@-` and `<-` give `curl_mime_data_cb(part1, -1, (curl_read_callback)fread, \` and `                    (curl_seek_callback)fseek, NULL, stdin);`, and `@-` adds filename `"-"`. `-F a=-` is plain data `"-"`. Each part is written in the order encoder, filename, name, type, and then `curl_mime_headers(part1, slistN, 1); slistN = NULL;` for `;headers=`. `name=(` ... `=)` and `@a,b` nest `mime2`/`part2` with `curl_mime_subparts(part1, mime2); mime2 = NULL;`.
- `-u user:pass`: `CURLOPT_USERPWD` after `FAILONERROR`. `-e http://ref/`: `CURLOPT_REFERER` before `USERAGENT`. `-A agent/1`: replaces the `USERAGENT` value (`-A ''` gives `""`).
- HTTP only (none of these appear for `ftp://`): `-L` gives `FOLLOWLOCATION` before `MAXREDIRS`, and `-e 'r;auto'` gives `AUTOREFERER` after it. After `MAXREDIRS` come `--compressed` (`ACCEPT_ENCODING, ""`), `-b a=1 -b b=2` (`COOKIE, "a=1; b=2"`), `-b jar.txt` (one `COOKIEFILE` per file, after `COOKIE`) and `-c jar.txt` (`COOKIEJAR`).
- `-I`: `NOBODY` after `NOPROGRESS` and `FILETIME` after `SSLVERSION`. `-R` also gives `FILETIME`. `-f` gives `FAILONERROR` after `NOBODY`. `--fail-with-body` writes nothing.
- `-o out.txt`, `-O` and `-i` write nothing.
- `-m 10`: `TIMEOUT_MS, 10000L` after `USERPWD` (`-m 1.5` gives 1500). `--connect-timeout 5`: `CONNECTTIMEOUT_MS, 5000L` after `CUSTOMREQUEST` (`2.5` gives 2500). A value of 0 writes nothing for either.
- `-4`/`-6`: `IPRESOLVE, 1L`/`2L` after `CONNECTTIMEOUT_MS`. `--resolve` and `--connect-to`: their own lists, `RESOLVE`/`CONNECT_TO` after `TCP_KEEPALIVE`.
- Bytes: `--data-binary` of `00 31 01 61 7f 80 0a 3f 00` gives `"\0001\001a\x7f\x80\n\?\x00"`. A non-printable byte before a hex digit is written in octal.
- Variables are numbered across `--next` groups in the order they are made and freed at the end.

Combined (`-X PUT -H 'X-A: 1' -d a=1 -u user:pass -L -e http://ref/ -A agent/1 -b a=1 -c jar.txt --compressed -f -o out.txt -i --connect-timeout 5 -m 10 --resolve h:47653:127.0.0.1 --connect-to h:47653:127.0.0.1:47653 -4 http://h:47653/f.txt`), the transfer's lines:

```
  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);
  curl_easy_setopt(curl, CURLOPT_URL, "http://h:47653/f.txt");
  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);
  curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L);
  curl_easy_setopt(curl, CURLOPT_USERPWD, "user:pass");
  curl_easy_setopt(curl, CURLOPT_TIMEOUT_MS, 10000L);
  curl_easy_setopt(curl, CURLOPT_POSTFIELDS, "a=1");
  curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)3);
  curl_easy_setopt(curl, CURLOPT_HTTPHEADER, slist1);
  curl_easy_setopt(curl, CURLOPT_REFERER, "http://ref/");
  curl_easy_setopt(curl, CURLOPT_USERAGENT, "agent/1");
  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 50L);
  curl_easy_setopt(curl, CURLOPT_ACCEPT_ENCODING, "");
  curl_easy_setopt(curl, CURLOPT_COOKIE, "a=1");
  curl_easy_setopt(curl, CURLOPT_COOKIEJAR, "jar.txt");
  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_2);
  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, "PUT");
  curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT_MS, 5000L);
  curl_easy_setopt(curl, CURLOPT_IPRESOLVE, 1L);
  curl_easy_setopt(curl, CURLOPT_TCP_KEEPALIVE, 1L);
  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist2);
  curl_easy_setopt(curl, CURLOPT_CONNECT_TO, slist3);
```

The full measured texts are pinned byte for byte in `Curl.Cli.UnitTests/LibcurlSourceCodeOptionTests.cs`: one data row per measured command line, three combined cases (HTTP, form and cookies, FTP), and two `--next` groups.

Decisions are recorded in ADR-0307: the order, defaults left out, HTTP-only lines, how variables are numbered, and octal before a hex digit. Default taken: `--json`'s headers are suppressed by a `-H` that starts with the same name followed by `:`, compared without regard to case. `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members (worst CRAP 10).

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --libcurl writes curl 8.21.0's setopt, slist and mime lines for the HTTP, output and connection options
