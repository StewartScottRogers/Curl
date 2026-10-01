# ADR-0308 — `--libcurl` source writes the HTTP, output and connection options as curl orders them

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-653.

## Context

ADR-0306 set up `LibcurlSourceCode`, which writes the skeleton `--libcurl` source. BL-653 adds the
lines for the HTTP, output and connection options. curl 8.21.0 (mingw, Schannel) was measured on
2026-10-01 with `Record-CurlExchange.ps1 --libcurl - -s`, one option at a time and then combined
(BL-653 Notes). curl writes each `curl_easy_setopt` call in the order its tool makes them. It skips a
number that equals libcurl's default, and it writes the HTTP-only options only for an `http` or `https` URL.

## Decision

1. **One fixed order per transfer**, taken from the combined measurements: `BUFFERSIZE`, `URL`,
   `NOPROGRESS`, `NOBODY`, `FAILONERROR`, `USERPWD`, `TIMEOUT_MS`, the body (`MIMEPOST` with its
   `curl_mime` calls, or `POSTFIELDS` and `POSTFIELDSIZE_LARGE`), `HTTPHEADER`, `REFERER`, `USERAGENT`,
   then the scheme's lines, `SSLVERSION`, `FILETIME`, `CUSTOMREQUEST`, `CONNECTTIMEOUT_MS`, `IPRESOLVE`,
   `TCP_KEEPALIVE`, `RESOLVE` and `CONNECT_TO`. For `http`/`https` the scheme's lines are
   `FOLLOWLOCATION`, `AUTOREFERER`, `MAXREDIRS`, `ACCEPT_ENCODING`, `COOKIE` (cookie strings joined with
   `; `), one `COOKIEFILE` per file, and `COOKIEJAR`. For `ftp`/`ftps` it is `FTP_SKIP_PASV_IP`. Any
   other scheme gets none of these.
2. **Defaults are not written.** A zero `-m` or `--connect-timeout`, an empty body's size,
   `--fail-with-body` (which curl's tool handles itself) and `-o`, `-O` and `-i` write no line, as in
   curl. `-I` and `-R` write `FILETIME`.
3. **`curl_slist` and `curl_mime` variables** (`LibcurlSourceVariables`) are numbered per kind across
   all transfers, in the order they are made. They are declared at the top, set to `NULL` and filled
   before `curl_easy_init`, and freed after `curl_easy_cleanup`. `--json` adds its `Content-Type` and
   `Accept` headers to `HTTPHEADER` after the `-H` ones, unless a `-H` already names that header.
4. **`-F` parts** follow curl's per-part order: content (`curl_mime_data`, `curl_mime_filedata`, the
   `fread` callback for standard input, or a nested `curl_mime` passed by `curl_mime_subparts`), then
   encoder, file name, name, type and headers. `<file` writes `curl_mime_filename(part, NULL)` and drops
   `;filename=`. `<-` writes no file name. `@-` is named `-` unless `;filename=` gives a name.
5. **Bytes outside printable ASCII** are written as `\xHH`. When a hex digit follows the byte, it is
   written as three octal digits instead (`\001a`), as curl does, so the C compiler does not read the
   digit as part of the escape. The `-d` family's body is quoted from its bytes, so a NUL in the body survives.
6. **`-G` moves the data out of the body**, so no `POSTFIELDS` is written. The query that curl adds to
   `CURLOPT_URL` is left to BL-654, along with every other option.

## Alternatives considered

- **Write every option the command line set, defaults included.** This was rejected because curl
  does not, and matching curl's source byte for byte is the reason the generator exists.
- **Keep one variable counter per transfer.** This was rejected because the measured two-group source
  numbers the lists `slist1` to `slist3` across both transfers and frees them all at the end.
