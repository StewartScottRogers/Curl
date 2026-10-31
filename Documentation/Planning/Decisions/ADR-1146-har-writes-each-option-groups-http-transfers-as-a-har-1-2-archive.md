# ADR-1146 — --har writes each option group's HTTP transfers as a HAR 1.2 archive

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4155.

## Context

BL-4155 adds HTTP Archive export as an extension (ADR-0999) in `Curl.Extension.Har.UnitLibrary`. curl has no such
option, so the extension is tested against the HAR 1.2 specification (W3C Web Performance Working Group draft,
"HTTP Archive (HAR) format", version 1.2), the version followed, and never changes what curl does without the
option.

## Decision

- **Option.** `--har <file>`, one value; curl has no option of that name. It is a transfer hook (ADR-1007),
  registered in the option, description and transfer hook catalogues; it has no URL scheme, so it adds no line to
  `ExtensionCatalogue`. With `CURL_EXTENSIONS=0` it is not registered, so curl refuses it as an unknown option.
- **One archive per option group.** Hooks are activated once per option group, so every HTTP transfer of the group
  (several URLs, `--next` starts another group) is an entry, in the order the transfers end. After each transfer the
  whole archive is written to the file again, so the file is complete even if a later transfer fails or curl is
  stopped; writes are serialised so parallel transfers (`-Z`) do not interleave.
- **Log (§log).** `version` `1.2`, `creator` `curl` with the extension assembly's version, and `entries`. `pages`
  is left out: it is optional and curl has no pages.
- **Entry (§entries).** `startedDateTime` is the transfer's `TimeProvider` UTC time when the hook starts it, in
  ISO 8601 with milliseconds; `time` is the sum of the `timings` phases that were taken, `ssl` excluded as the
  specification says; `cache` is empty (curl keeps no cache); `serverIPAddress` comes from the report's remote end
  point when known. `connection` is left out: curl reports no connection identifier.
- **Request (§request).** The method is the report's, else `-X`, else the one curl's options imply (`HEAD` for `-I`,
  `PUT` for `-T`, `POST` for a body, `GET`). The URL is the effective URL, else the URL typed. The headers are those
  the hook can see: `User-Agent` and `Referer` from curl's options and every `-H` field, a `-H` field of the same
  name winning and `Name:` or `Name;` (which remove or empty a field) left out; the fields curl's handler adds
  itself (`Host`, `Accept`, `Content-Length`) are not visible to a transfer hook and are not listed.
  `queryString` is the URL's query split on `&` and `=`, `+` read as a space and percent-decoded. A `-d` body is
  `postData` with its `Content-Type` and its text read as UTF-8; `headersSize` is -1 (unknown); `bodySize` is
  the `-d` body's length, else 0.
- **Response (§response, §content).** `status` is the response code; `statusText` is empty, as curl's report keeps
  no reason phrase; `httpVersion` is `HTTP/1.0`, `HTTP/1.1`, `HTTP/2` or `HTTP/3`, the names curl prints; headers
  are the final response's. `content` holds the body as curl's handler wrote it, before curl's output processing:
  its `text` when the bytes are valid UTF-8, otherwise base64 with `encoding` `base64`; `mimeType` is the content
  type, else empty. `redirectURL` is the report's redirect URL, else empty; `headersSize` is curl's header size
  and `bodySize` the body's length. A transfer that got no response (a failed connect) is written with status 0 and
  sizes -1, as browsers write one, and keeps its own exit code.
- **Timings (§timings).** From `TransferTimings`, in milliseconds to three decimals: `blocked` from the handler's
  start to the connector's, `dns`, `connect` (which includes `ssl`), `ssl`, `send`, `wait` and `receive`. An
  optional phase not taken is -1; the required `send`, `wait` and `receive` are 0 when not recorded.
- **Not HTTP.** A transfer that is not HTTP passes through unrecorded: HAR describes HTTP exchanges only.
- **Write failure.** A file that cannot be written fails the transfer with exit 23 (`CURLE_WRITE_ERROR`), as curl
  fails a transfer whose output cannot be written, and keeps the transfer's report.

## Left out, and why

- **`cookies` arrays** are written empty: the cookies are already in the `Cookie` and `Set-Cookie` headers, and the
  specification allows an empty list.
- **Earlier responses of a followed redirect** are not separate entries: a transfer hook sees one transfer per URL,
  and the report keeps only the final response's headers.
- **Comments and the optional `_`-prefixed custom fields** are not written.

## Consequences

The archive is in memory until it is written, as is each response body, so a very large download costs its size
in memory while `--har` is given. Without `--har`, no hook is activated and every transfer is unchanged.
