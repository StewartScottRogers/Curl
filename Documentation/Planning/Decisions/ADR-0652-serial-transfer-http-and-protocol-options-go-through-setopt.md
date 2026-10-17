# ADR-0652 — A serial transfer's HTTP and protocol options, and its `-F` form, go through `curl_easy_setopt`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2493.

## Context

ADR-0631 and ADR-0642 moved six of a serial transfer's `EasyOptions` groups onto `CurlEasy.SetOption` and left the
HTTP and protocol groups whole on the engine transfer's hooks. Upstream's `config2setopts.c` sets those with one
`curl_easy_setopt` call each too, and the `-F` form as a `curl_mime` tree through `CURLOPT_MIMEPOST`. Libcurl's
setters (`Libcurl/Options/Http`, `Libcurl/Options/Protocols`, `Libcurl/Mime`) refuse, cut or widen some values, and
the output must stay byte for byte what it is.

## Decision

1. `EasySetoptsFromCommandLine.Of` also makes one `EasySetopt` per HTTP and protocol field with a `CURLOPT_*` whose
   value differs from libcurl's default, after the proxy group (`EasySetoptsFromCommandLine.HttpProtocols.cs`): the
   HTTP rows in `EasyHttpOptions`' order (`CURLOPT_POSTFIELDS` before `CURLOPT_POSTFIELDSIZE_LARGE`, as the tool sets
   them), one `CURLOPT_COOKIEFILE` per `-b` file (the setter adds one file per call), then the FTP, SSH, mail, TFTP,
   TELNET, RTSP and WebSocket rows. Lists go as `string[]`, flags as `0L`/`1L`, enums as their `long`.
2. The `-F` form is built by `CurlMimeFromFormBody` with `curl_mime_init` on the runner's library
   (`ToolCurlEngine.MimeInit`): per part `curl_mime_name`, the data (`curl_mime_data`, `curl_mime_filedata` or a
   nested `curl_mime_subparts`), then `curl_mime_filename` (so `curl_mime_filedata`'s base-name default is replaced by
   the form's name, or none for `<file`), `curl_mime_type`, `curl_mime_headers` and `curl_mime_encoder`. The runner
   sets it as `CURLOPT_MIMEPOST` after the groups' calls and frees it once set, since the setter takes the body as
   the parts are then. A missing `@file` is still set: `curl_mime_filedata`'s `CURLE_READ_ERROR` is ignored, so the
   transfer fails reading it as it does today.
3. These stay on the hooks (`EasySetoptsFromCommandLine.KeptOnHooks`), so the transfer is unchanged:
   - a form with a `@-` or `<-` part (the tool's own standard input reader; `curl_mime_data_cb` would make a
     callback part the engine reads differently) or an encoder `curl_mime_encoder` refuses: the whole form, as
     `MimePost`;
   - `HttpPost`, `StreamDependency` and `CookieList`, which the command line never sets;
   - a `--max-redirs` or `POSTFIELDSIZE` below -1 (refused), an `--expect100-timeout` that is not a whole number of
     milliseconds up to `INT_MAX` (cut), a stream weight outside 1 to 256 (ignored), a `--tftp-blksize` outside 8 to
     65464 (widened or cut) and a `--hostpubmd5` that is not 32 characters (refused; the parser already refuses one).
4. A non-`CURLE_OK` answer ends the transfer as ADR-0631's setopt failure path does; none of these calls can answer
   one for a command line the parser accepts.

## Consequences

Every option group but the callbacks now reaches the engine through `curl_easy_setopt`, as upstream's tool hands its
options to libcurl. A Libcurl setter that answers differently from the record is a Libcurl task, not a change here;
none was found.
