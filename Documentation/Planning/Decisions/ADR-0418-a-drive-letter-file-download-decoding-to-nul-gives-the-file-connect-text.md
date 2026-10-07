# ADR-0418 — A drive-letter file:// download decoding to NUL gives file_connect's text

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1526
- Decided by Claude under Stewart's delegation.
- Corrects: ADR-0416, Context's drive-letter bullet and Decision 3.

## Context

ADR-0416 recorded that on Windows `curl -sSv file:///C:/<dir>/f.txt%00x` writes
`* URL rejected: Malformed input to a URL function` and exits 3, and filed BL-1526 to
imitate it in URL parsing. Re-measured on Windows on 2026-10-07 with curl 8.21.0
(`libcurl/8.21.0 Schannel`):

- `curl -sSv "file:///C:/bl1526tmp/f.txt%00x"` writes only
  `curl: (3) URL using bad/illegal format or missing URL` - the same as the drive-less form.
- `curl -sSv "file:///C:/bl 1526tmp/f.txt"` writes `* URL rejected: Malformed input to a URL
  function` and `curl: (3) URL rejected: Malformed input to a URL function`.

The `<dir>` of the first measurement was under `C:\Users\Stewart Rogers\`, and its space is
what `Curl_junkscan` (`lib/urlapi.c` at `curl-8_21_0`, line 243) refuses with
`CURLUE_MALFORMED_INPUT`, which `parseurlandfillconn` (`lib/url.c` line 2255) reports as
`URL rejected`. Neither `lib/urlapi.c`'s drive-letter handling (lines 934-950) nor `lib/url.c`
decodes the path; the `%00` reaches `file_connect` (`lib/file.c` line 163, `REJECT_ZERO`)
with or without a drive letter. The upload measured with the same space-holding directory gave
`URL using bad/illegal format or missing URL` for another reason: with `-T` the tool parses
the URL first (`add_file_name_to_url`, `src/tool_operhlp.c`) and turns the parser's refusal
of the space into a bare `CURLE_URL_MALFORMAT` with no `failf`.

## Decision

1. No drive-letter special case: `CurlUrl` accepts `file:///C:/dir/f.txt%00x` and
   `FileProtocolHandler` refuses it with ADR-0416's text, on Windows as elsewhere.
2. `Curl.Protocol.Abstractions.UnitTests` pins both halves: the drive-letter `%00` URL is
   accepted by the parser, and a space in a drive-letter path is `MalformedInput`.

## Consequences

ADR-0416's third decision and its last consequence no longer describe a gap: Curl already
matches curl 8.21.0 for the drive-letter download.
