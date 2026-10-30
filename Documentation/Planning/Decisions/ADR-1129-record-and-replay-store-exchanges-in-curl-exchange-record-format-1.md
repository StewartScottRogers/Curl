# ADR-1129 — Record and replay store exchanges in Curl exchange record format 1

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4157.

## Context

BL-4157 adds record and replay as an extension (ADR-0999) in `Curl.Extension.RecordReplay.UnitLibrary`: one option
that records each exchange to a directory and one that answers transfers from such a directory without the network.
curl has no such options and there is no standard record format a command-line HTTP client shares, so this ADR
defines the format, Curl exchange record format 1, and the extension is tested against its sections. Without either
option nothing changes what curl does. ADR-1019's transfer hook may answer a transfer itself or capture curl's
response, which is all record and replay need.

## Decision

- **Options.** `--record <dir>` and `--replay <dir>`, each taking one directory; curl has neither. They are one
  transfer hook (ADR-1007), registered in the option, description and transfer hook catalogues; the extension has
  no URL scheme, so, as ADR-1106's `--ratelimit-pace` and ADR-1126's `--mldsa-verify`, it adds no line to
  `ExtensionCatalogue`. Its description name is `record-replay`.
- **Scope.** HTTP and HTTPS transfers (those with HTTP request options). Any other transfer runs as it would without
  the hook.

### Curl exchange record format 1

1. **Directory.** One file per request, named `<key>.exchange`, in the given directory, which the first recording
   creates. A recording replaces the file of the same key by writing a temporary file and moving it into place, so a
   reader sees the old exchange or the new one, never half of one.
2. **Key.** The request's method and the URL as the transfer was given it form the Latin-1 line
   `<method> <url>` and an LF; the `-d` body bytes follow; the key is the lowercase hexadecimal SHA-256 of those
   bytes. The method is `-X`'s when given, else `HEAD` for `-I`, `POST` for a `-d` or `-F` body, `PUT` for `-T`, and
   `GET` otherwise.
3. **File.** The Latin-1 line `CURL-EXCHANGE 1`, the line `<method> <url>`, the line
   `<status> <head length> <body length>` (decimal, no sign), each ended by one LF; then the head bytes, the response
   header bytes exactly as curl wrote them for `-i` (every status line, including interim and redirect responses
   curl showed); then the body bytes; nothing after. A file that is not exactly that reads as no recording.
4. **Recording** (`--record`). The transfer goes out as usual and its response is written to the outputs as
   received. When the transfer succeeded and a response arrived, it is recorded under the request's key; a failed
   transfer records nothing. A directory that cannot be written leaves the transfer as it was.
5. **Replaying** (`--replay`). A request whose key has a recording is answered from it without contacting the
   server: the head goes to the `-i` and `-D` outputs, the body to the output, and the transfer reports the recorded
   status, the header fields after the last status line, the method and the URL. A request with no recording fails
   with exit 7 (`CURLE_COULDNT_CONNECT`, the nearest of curl's codes to "no server to reach") and the message
   `No recorded exchange for <method> <url> in <dir>`, unless `--record` is given too: then it goes out and is
   recorded there, so `--replay d --record d` fills in what a directory lacks.

## Left out, and why

- **Request headers and `-F` bodies in the key.** Headers carry dates, tokens and user agents that change between a
  recording and its replay, so matching on them would make most replays miss; a `-F` body's multipart boundary is
  random each run for the same reason. Requests that differ only in those replay the same exchange.
- **Timings.** A replay reports no connect or transfer times: there was no connection.
- **HAR or WARC.** Both are richer formats whose readers want fields (timings, request headers, WARC records per
  connection) a transfer hook does not see; a byte-exact head and body is what a replay needs to give curl's output
  back unchanged.
- **Other protocols.** FTP, SMTP and the rest have no request/response pair a key can name as simply; they run as
  without the hook.
