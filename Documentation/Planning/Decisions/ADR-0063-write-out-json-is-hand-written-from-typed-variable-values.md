# ADR-0063 — `%{json}` and `%{header_json}` are hand-written from typed variable values

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-227, 2026-09-27).

## Context

`-w '%{json}'` prints every `-w` variable as one JSON object and `-w '%{header_json}'` the
response headers. Measured on 2026-09-27 with curl 8.21.0 (mingw, Schannel); the commands
and bytes are in BL-227's Notes:

- `%{json}` is one line: the variables in ordinal name order (`url` < `url.fragment` <
  `url_effective` < `urle.fragment`), then `curl_version`, the libcurl version string.
- Numbers are unquoted, and a status code loses its padding (`"http_code":0`, where
  `%{http_code}` prints `000`). Times print as seconds with six decimals, unquoted.
- A text value curl has no value for is `null` (`content_type`, `errormsg` on success,
  `filename_effective` to standard output, `referer`, `redirect_url`, `ftp_entry_path`,
  `scheme` for an unsupported scheme, a URL part the URL lacks, `url.port` for a scheme curl
  does not know); `local_ip`, `remote_ip` and `certs` print `""`.
- `%{json}` knows `size_delivered`, which the text path did not.
- Strings escape `"` and `\`, `\b` `\f` `\n` `\r` `\t` by name, any other character below
  U+0020 as `\u` with four lower-case hex digits, and pass everything else through.
- `%{header_json}` lower-cases each name, groups every value of a name into one array at
  the name's first appearance, trims values of spaces and tabs, separates members with a
  comma and a line feed, and ends with a line feed and `}`; no headers print `{`, line feed,
  `}`.

## Decision

1. Each variable formatter in `TransferWriteOutVariables` returns a `WriteOutValue`: its
   `%{name}` text and its JSON value. Text values are built from a nullable string, so
   "no value" prints nothing as text and `null` in JSON.
2. `json` and `header_json` are variables of `TransferWriteOutVariables`, not special
   cases in `WriteOutTemplateRenderer`: they read only the transfer, and the renderer's
   text-mode line-feed handling already turns `header_json`'s line feeds into CR LF on
   Windows, as the mingw curl writes them.
3. The JSON is hand-written in `WriteOutJson` (base class library only; no serializer).
4. `curl_version` comes from `TransferWriteOutVariables.LibraryVersion`, which defaults to
   the library part of this tool's own `-V` line (ADR-0021): `libcurl/8.21.0 Schannel`,
   `SecureTransport` or `OpenSSL` by platform. Claiming zlib, zstd, libidn2 or libssh2
   versions this tool does not link would be false.
5. `size_delivered` prints the `size_download` value.

## Consequences

- `%{json}` matches curl byte for byte except for `curl_version`'s library list and the
  timing values this tool measures differently (`time_queue` is one microsecond, ADR-0035).
- `size_delivered` differs from curl's under `--compressed` until the report carries a count
  of decoded bytes.
- Header values reach the output as UTF-8 of the decoded string; curl writes the raw header
  bytes, so a non-ASCII header byte may print differently.

## Alternatives considered

- **`System.Text.Json` `Utf8JsonWriter`:** its escaping differs (it escapes `"` as
  `"` and non-ASCII by default) and would need a custom encoder to match; hand-writing
  is shorter and exact.
- **Rendering JSON in `WriteOutTemplateRenderer`:** it would need a typed view of every
  variable through `IWriteOutVariableSource`, widening the interface for one caller.
- **The reference build's full `curl_version` string:** names libraries this tool does not
  contain.
