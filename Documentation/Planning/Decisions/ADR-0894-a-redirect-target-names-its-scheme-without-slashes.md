# ADR-0894: A redirect target names its scheme with or without `//`

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3547
- Decided by Claude under Stewart's delegation.

## Context

Third-party gap report issue CGR-018: a 302 with `Location: localhost:20200/x` was followed by
`-L` as `http://localhost:20200/x`, because `RedirectFollower` parsed the target with
`CurlUrl.TryParse`, which guesses a scheme for text with no `://`, as a command-line URL has its
scheme guessed. curl 8.22.0 (Schannel, Windows; measured 2026-10-26 with
`Record-CurlExchange.ps1`) reads `localhost` as the target's scheme and refuses it: exit 1,
`curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme`, one request,
`%{url_effective}` the first URL. `Location: foo:bar` gives the same, and
`Location: http:/127.0.0.1:18731/y` is followed.

## Decision

Before parsing a redirect target, `RedirectFollower` reads any RFC 3986 scheme and colon at its
start (a letter, then letters, digits, `+`, `-` or `.`) as the target's scheme, with or without
`//`. A scheme curl does not parse (`SchemesCurlParses`, compared lowercased) is refused with
exit 1, `The redirect target URL could not be parsed: Unsupported URL scheme`, before the
target is parsed. On Windows a single letter and a colon is a drive prefix, not a scheme, as
`UrlSchemeGuesser` already treats it.

## Consequences

- A redirect target never has its scheme guessed when it starts with scheme-like text; a target
  starting with no such text is parsed as before.
- The check of the parsed URL's scheme after `CurlUrl.TryParse` could no longer fail and was
  removed.
