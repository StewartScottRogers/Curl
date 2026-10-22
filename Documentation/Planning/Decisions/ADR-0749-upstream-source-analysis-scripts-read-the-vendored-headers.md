# ADR-0749 — Upstream's symbol and error-code scripts read the vendored headers, not `Libcurl`'s tables

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2991
- Decided by Claude under Stewart's delegation

## Context

Upstream cases 1119, 1175 and 1477 run `tests/test1119.pl`, `test1175.pl` and `test1477.pl`, which
check curl's own source tree: that `include/curl/*.h`, the libcurl man pages,
`docs/libcurl/symbols-in-versions` and `libcurl-errors` name the same symbols and error codes.
BL-2991 planned for `Libcurl`'s tables (`CurlOptions.All`, the error-text tables) to stand for the
headers. But test1119 counts every `#define CURL...` and enum member of every header - version
macros, `CURLAUTH_*`, `CURLSSLOPT_*`, lock data, callback return codes and more, about 1,100 names
- and `Libcurl` has tables only for options and error texts, keyed by number, with no
`CURLUE_`, `CURLSHE_`, `CURLHE_` or `CURLM_` names. Standing its tables in would report hundreds of
"missing" symbols that are no defect of Curl. BL-2990 vendored curl 8.22.0's `include/curl` headers
beside the docs.

Two further gaps: test1119 runs the C preprocessor on each header, and test1477 reads
`docs/libcurl/libcurl-errors.3`, which upstream's build generates from `libcurl-errors.md` and the
release tarball does not hold.

## Decision

- The C# interpretations (`UpstreamTest1119Script`, `UpstreamTest1175Script`,
  `UpstreamTest1477Script`) read the vendored headers, as the scripts do. These cases check
  upstream's tree for consistency; the vendored files are that tree.
- In place of the preprocessor, test1119's interpretation strips comments and preprocessor lines,
  keeps every `#if` branch, and reads a line that starts with a call of a function-like macro the
  headers define (`CURLOPT(CURLOPT_URL, ...)`, `CURLOPTDEPRECATED(...)`) from its first argument,
  which is what those macros expand to first. With the 8.22.0 headers it prints `OK`, as the script
  does.
- test1477's interpretation reads `libcurl-errors.3` from the build root when it exists and
  otherwise the `## NAME` headings of `libcurl-errors.md`, which `cd2nroff` turns into the
  `.IP "NAME` lines the script reads.

## Consequences

Cases 1119, 1175 and 1477 pass and are on the ratchet. They hold the vendored files consistent, not
`Libcurl`'s tables; a check of `Libcurl`'s names against the headers would be its own unit test in
`Libcurl.UnitTests`, not these cases.
