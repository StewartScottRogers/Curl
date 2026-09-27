# ADR-0049 — The Public Suffix List is an embedded, dated snapshot refreshed by a script

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (Phase 1 HTTP plan, item D6, 2026-09-26;
recorded by BL-156, 2026-09-27).

## Context

curl refuses a cookie whose `Domain` is a public suffix (`co.uk`, `github.io`) when it is
built with libpsl. The Windows reference build (ADR-0018) is: `curl -V` for
`/mingw64/bin/curl` 8.21.0 lists `libpsl/0.21.5` and the `PSL` feature, and BL-223
reproduces that check in `Curl.Cookies.UnitLibrary`. It needs the list itself, and the list
has to come from somewhere.

Measured on 2026-09-27 with the reference build's own libpsl
(`/mingw64/bin/psl.exe --print-info`): libcurl uses the list compiled into libpsl, with no
distribution file on disk (`dist filename` is empty). That built-in list holds 9556
suffixes, 8 exceptions and 126 wildcards, from a `public_suffix_list.dat` dated
2024-01-13 (SHA-1 `d60ac92ab2057010278034ae502fe2b3d50f8068`). `psl.exe --use-builtin-data
co.uk example.com` answers `co.uk: 1`, `example.com: 0`. Linux and macOS curl builds link
the system libpsl and read whatever list the distribution ships, so no single dated list is
"curl's list" across platforms.

The list is published at https://publicsuffix.org/list/public_suffix_list.dat under the
Mozilla Public License 2.0. Curl's code is MIT (`LICENSE.txt`); MPL-2.0 is file-level, so
the unmodified file can ship beside MIT code as long as its licence notice stays with it
and recipients of the binary are told where the source form is.

## Decision

1. **Source.** The list is https://publicsuffix.org/list/public_suffix_list.dat, the
   ICANN and PRIVATE sections both, as libpsl uses both for its cookie check.
2. **Resource location.** One file,
   `Curl.Cookies.UnitLibrary/PublicSuffixList/public_suffix_list.dat`, included as an
   `EmbeddedResource` in `Curl.Cookies.UnitLibrary.csproj`. With the project's root
   namespace `Curl.Cookies` its manifest name is
   `Curl.Cookies.PublicSuffixList.public_suffix_list.dat`, read with
   `Assembly.GetManifestResourceStream`, which is native-AOT safe.
3. **Dated.** The file is the upstream bytes unchanged except for one line the refresh
   script puts first: `// Curl snapshot: yyyy-MM-dd, from https://publicsuffix.org/list/public_suffix_list.dat`.
   It is a PSL comment, so the parser skips it; it is how a reader, a test or `git log`
   tells how old the snapshot is.
4. **Parsed by hand.** A small parser in `Curl.Cookies.UnitLibrary` reads the lines once,
   on first use: `//` comments and blank lines skipped, `*.` wildcard rules, `!` exception
   rules, and the rest plain rules. Base class library only; no package.
5. **Refreshed by a script, not by the build.** `Update-PublicSuffixList.ps1` at the
   repository root, listed as a loose file in the `Scripts` solution folder of `Curl.slnx`,
   downloads the list, writes the dated line and the upstream bytes to the resource path,
   and is ASCII-only. A task runs it when a refresh is wanted and commits the result; the
   build never touches the network.
6. **Attribution.** The MPL-2.0 notice lives in the snapshot file itself, where upstream
   put it, and is never stripped:

   > This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
   > If a copy of the MPL was not distributed with this file, You can obtain one at
   > https://mozilla.org/MPL/2.0/.

   Beside it, `Curl.Cookies.UnitLibrary/PublicSuffixList/README.md` states: "The Public
   Suffix List (`public_suffix_list.dat`) is maintained by the Public Suffix List project
   at https://publicsuffix.org/ and is used under the Mozilla Public License 2.0
   (https://mozilla.org/MPL/2.0/). Curl embeds it unmodified, apart from a dated comment
   line added first; its source form is this file and
   https://publicsuffix.org/list/public_suffix_list.dat." The script leaves that README
   alone.

## Consequences

- Cookie checks are deterministic and offline: every test and every run on a given commit
  uses the same list, and no system file or network is read.
- The published native binary carries the whole list as text inside it.
- A refreshed snapshot is newer than the 2024-01-13 list inside the reference build's
  libpsl, so a suffix added upstream after that date is refused by Curl and accepted by the
  reference curl. That is accepted: the newer list is what a current Linux or macOS curl
  reads, and pinning the old one would freeze Curl to one build's packaging date. A test
  that pins a curl comparison uses a suffix present in both lists.
- The list goes stale between refreshes; freshness depends on someone filing a task to run
  the script.
- Adding the resource, the README and the script is BL-222; the matching logic is BL-223.

## Alternatives considered

- **Read the system list at run time** (`/usr/share/publicsuffix`, as libpsl's
  `psl_latest` does). Windows has no such file, output would vary by machine, and tests
  would depend on the host.
- **Download the list at build time.** Builds would need the network and would not be
  reproducible from a commit.
- **Pin the list libpsl 0.21.5 compiled in (2024-01-13).** Matches one Windows build
  exactly but is already stale and matches no Linux or macOS curl; a dated snapshot that a
  script can refresh serves the drop-in goal better.
- **Generate C# source (a `HashSet` literal or a DAFSA, as libpsl does) from the list.**
  Faster lookups, but the committed artefact is no longer the upstream file, the MPL notice
  has to be carried into generated code, and a 10 000-entry literal is slow to compile. Hand
  parsing once at first use is cheap enough for a command-line tool.
- **Take a package that bundles the list.** Breaks the base-class-library-only rule and
  would need Stewart's approval for a few dozen lines of parsing.
