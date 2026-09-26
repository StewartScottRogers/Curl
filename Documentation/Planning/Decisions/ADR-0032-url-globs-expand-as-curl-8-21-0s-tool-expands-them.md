# ADR-0032 — URL globs expand as curl 8.21.0's tool expands them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-207 needs curl's URL globbing - `{a,b}` sets, `[1-10]`, `[01-10]` and `[a-z:2]` ranges,
`#N` in `-o`, and `-g` to turn it off - before `Curl.Console` can wire it (BL-240). Open
questions: where it lives, how far to follow curl's parser (including its quirks), how a
bracketed IPv6 host is told apart from a range, and what `-g` means for `#N`.

Every rule below was measured with the reference build (curl 8.21.0, mingw, Schannel,
`/mingw64/bin/curl`) on 2026-09-26 with
`curl -s -S -w '%{url}|%{filename_effective}\n' -o '<name>' '<url>'` over `file:///n/...`,
which fails each transfer with exit 37 but prints every expanded URL and output name. The
cases are pinned in `UrlGlobTests` in `Curl.Core.UnitTests`.

## Decision

- **One type in `Curl.Core.Globbing`.** `UrlGlob.TryParse` parses; `UrlGlob.Unglobbed` is
  `-g`; `Expand()` yields `UrlGlobMatch`es (URL plus glob values) lazily, rightmost glob
  fastest, so a range of billions costs nothing until walked; `UrlCount` is the product.
  BL-031 (`-T` globs) can reuse it.
- **The parser is `tool_urlglob.c`, quirks included.** Messages and columns are curl's:
  `bad range`, `bad range specification`, `unmatched brace`, `nested brace`,
  `empty string within braces`, `unexpected close bracket`,
  `unmatched close brace/bracket`, `range end/step overflow` and `range overflow`, as
  `<reason> in position N:\n<url>\n<caret line>`, exit 3 (`UrlMalformat`). A set's
  `range overflow` has no position. Columns point where curl's scanner stopped, and every
  closed `{...}` set before the error moves the column one further left, because curl
  moves past `}` without counting it. The caret line is at least one space and `^`, as
  `%*s^` prints it.
- **No glob limit.** 100 consecutive sets expand; 8.21.0 has no pattern cap.
- **IPv6 literals by shape, not by libcurl's URL parser.** Curl skips a `[...]` that its
  URL API accepts as a host. We accept the bracketed text when it is hexadecimal digits,
  colons and dots that `IPAddress` reads as IPv6, optionally followed by `%` or `%25` and a
  zone of unreserved characters, and shorter than 128 characters overall; `[]` is literal
  too. Every measured case agrees; a pathological address the two parsers judge
  differently is the accepted risk of not porting `urlapi.c`.
- **`#N` is `glob_match_url`.** The whole digit run is N (`#01` is glob 1); `#0`, a number
  past the last glob, or one too long to read stays as written.
- **`-g` means no glob values.** Under `-g` the one URL is taken as written and every `#N`
  stays as written, as measured.
- **Windows name sanitizing is separate.** On Windows curl runs `sanitize_file_name` on the
  substituted name; that step is BL-283, not part of `SubstituteGlobValues`.

## Consequences

`Curl.Console` (BL-240) parses each URL with `UrlGlob.TryParse` unless `-g` (BL-282) is
set, prints the failure as `curl: (3) <message>` when not silent, and runs one transfer per
match with `SubstituteGlobValues` applied to its `-o` name.
