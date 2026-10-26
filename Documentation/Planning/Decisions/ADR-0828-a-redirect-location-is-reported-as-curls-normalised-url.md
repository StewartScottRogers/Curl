# ADR-0828: A redirect Location is reported as curl's normalised URL

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3591)
- Relates to: [ADR-0824](ADR-0824-sspi-digest-reads-and-answers-challenges-as-windows-curl-does.md)

## Context

CGR-062: after a redirect, curl 8.22.0 prints `%{redirect_url}` and `%{url_effective}` as its URL
parser rebuilds the `Location`, where Curl printed the text as sent. Measured with
`Record-CurlExchange.ps1` against curl 8.22.0 Schannel on Windows (BL-3591 Notes):
`https:/x` is `https://x/`, `http:/b` is `http://b/`, `/a/%2e%2e/b` is `/b` (and is sent as
`GET /b`), `file:///c:/windows/win.ini` is `file://c:/windows/win.ini` on Windows (unchanged on
Linux), and following `http:` fails with `Unsupported number of slashes following scheme`.

## Decision

`HttpRedirectLocation` normalises the resolved target as curl does: a scheme other than `file`
followed by one or more slashes takes its host after them, a dot segment written `%2E` (any case)
is removed as a dot is, and, with Windows drive-letter rules, a `file` path naming a drive loses
its authority and the slash before the drive. The follower takes that text as the next target, so
`%{url_effective}` follows. `RedirectFollower` gives curl's slashes reason for a target with no
slash after its scheme. The drive rule is a parameter (`driveLetters`), so both platforms' answers
are tested on either.

## Consequences

A `Location` that already matched is unchanged. A target with four or more slashes is still
refused by the URL parser, as curl refuses it.
