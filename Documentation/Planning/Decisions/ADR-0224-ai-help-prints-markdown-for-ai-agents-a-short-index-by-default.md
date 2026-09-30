# ADR-0224 — `--ai-help` prints Markdown for AI agents, a short index by default

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-911.

## Context

Stewart asked on 2026-09-29 for help aimed at AI agents beside the human `--help`, printed
as Markdown. `--help` and `--manual` print curl 8.21.0's pages byte for byte and are laid
out for a 79-column terminal: justified, wrapped, and split across a usage page, category
pages and a 7849-line manual. An agent reads that at the cost of its context window.

Real curl has no such option. The Windows system curl 8.21.0 (Schannel) refuses it,
measured 2026-09-29:

```
curl: option --ai-help: is unknown
curl: try 'curl --help' for more information
```

and exits 2. No working script can depend on that refusal, so accepting the option breaks
no existing caller.

## Decision

1. **The option.** `--ai-help`, two dashes, no short letter. A single-dash `-aihelp`
   cannot work: curl reads it as the bundled short options `-a -i -h -e -l -p`. It is
   parsed as `--help` is (`CommandLineOption.Subject`): it ends parsing where it stands,
   takes its subject from the attached value or else the next argument, and exits 0.
   `--no-ai-help` is refused as not reversible, as `--no-help` is. An `ai-help` line in a
   `-K` file is ignored, as `manual` and `version` lines are.
2. **Index by default.** With no subject it prints a short index (about 60 lines): what
   this curl is, each category with its one-line description and the command that expands
   it, the 20 most-used options with their short form, argument and summary, and how to
   ask for more. The full reference covers about 275 options and is 470 KB; an agent asks
   for it only when it needs it.
3. **A category, or `all`.** `--ai-help <category>` (any case) prints a `#` heading for
   the category and a `##` section for each of its options: short form, argument, the
   `--no-` spelling that reverses it, how a repeat behaves, other accepted spellings, a
   note when this build does not parse it yet, the one-line summary, then the option's
   section of the manual. `all` prints every category that way, then the exit codes and
   the `--write-out` variables.
4. **One source.** Everything is generated from the tables `--help` and `--manual` print
   from (`CurlHelpTable`, `CurlHelpText.Categories`, `CurlOptionAliasTable`,
   `CommandLineOptionTable`, `CurlManual.txt`), so the two can never disagree. The manual's
   terminal layout is undone: paragraphs are joined onto one line, justifying spaces
   collapsed, examples become fenced code blocks, and term lists (exit codes,
   `--write-out` variables) become Markdown lists.
5. **Same bytes everywhere.** Lines end in `\n` on every platform, not the console
   newline, so the Markdown is identical on Windows, Linux and macOS.
6. **An unknown category** prints what `--help <unknown>` prints — `Unknown category
   provided, here is a list of all categories:` and the category list — with the platform
   newline, and exits 0. So does `category` and a subject starting with `-`: the index
   already lists the categories, and one option's section is one category away.
7. **Every option has a place.** A unit test fails when a row is added to
   `CommandLineOptionTable` without appearing in `--ai-help all`. Names the help table
   lists under another name (`--include`, `--ftp-ssl`, `--ftp-ssl-reqd`, `--krb4`,
   `--eprt`, `--epsv`) are carried as a line of the section they belong to.

## Consequences

- An additive departure from drop-in behaviour: `curl --ai-help` succeeds here and fails
  with exit 2 in real curl. Nothing else changes; `--help` and `--manual` stay byte for byte.
- Every task that adds or changes an option keeps `--ai-help` right; the unit test enforces
  the listing, the shared tables keep the wording.
- How a repeat behaves is read from the sentences curl's manual generator writes for each
  option ("can be used several times", "the last set value is used", "has no extra
  effect", "associated with a single URL"); an option whose section has none says "not
  stated; see the description".
