# ADR-0752 — test1222's interpretation reads a libcurl manual converted in memory from the vendored Markdown

- Status: Accepted
- Date: 2026-10-21
- Task: BL-3000
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 1222 runs `tests/test1222.pl`, which checks that the public headers, the libcurl man pages
(`docs/libcurl/*.3` and `docs/libcurl/opts/*.3`) and `docs/libcurl/symbols-in-versions` agree on every
deprecation. The `.3` pages are written at build time by `scripts/cd2nroff` from `docs/libcurl/*.md` and
`docs/libcurl/opts/*.md`; no release tarball has them, so the vendored data (BL-2990) holds only the
Markdown, and BL-2992's interpretation took the script's "curl built without the libcurl manual" skip.

## Decision

- `UpstreamTest1222Script` reads built `.3` pages from `BUILDROOT/docs/libcurl` when there are any, as the
  script does.
- With none, it reads the pages `UpstreamCd2nroff`, a C# port of `cd2nroff`, converts in memory from
  `ROOT/docs/libcurl`'s Markdown, keyed by their built paths (`NAME.3`, `opts/NAME.3`). Nothing is written
  to disk and nothing generated is committed.
- The skip line stays for a release with neither built pages nor Markdown function pages.
- The port keeps every line the check reads (`.IP`, `.PP`, `.SH`, `.nf`/`.fi` blocks, the inline escapes
  and font changes, the blank lines) and leaves out what it never reads: the `.TH` date and source, the
  generated-by comment, the PROTOCOLS prose and the front-matter validation.
- Where Perl walks a hash or a folder in undefined order (the headers, the function pages), the
  interpretation goes in ordinal order.

## Why

The rule is to match upstream's behaviour from the release data alone, with the BCL only. A real build
always has the manual, so case 1222 checks it there. Converting the Markdown as `cd2nroff` does is the
build step itself, so the check runs on the same pages a build would have. ADR-0750 does the same for
`curl.1` in case 1139. Keeping the pages in memory means no generated files to commit or keep current.

## Consequences

Case 1222 now runs the full deprecation check over the vendored 8.22.0 headers, manual and
symbols-in-versions, and passes with nothing to report. A later vendored release whose sources disagree
fails the case with the script's table.
