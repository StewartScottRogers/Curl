# ADR-0069 — Help pages come from curl's own help table, and the manual is embedded

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-201 asks for `-h`, `--help <category>`, `--help all` and `-M` to print what curl 8.21.0
prints, byte for byte, "generated from the option table where possible". Measured on the mingw
reference build (`/mingw64/bin/curl`, 2026-09-27):

- `-h` prints a usage page of the options curl marks important, then the category names wrapped
  to the terminal width; `--help all` prints all 274 help rows; `--help <category>` prints a
  heading and that category's rows; `--help category` lists the 25 categories; any other subject
  prints `Unknown category provided, ...` and the list. Subjects match in any case.
- The option column's width depends on the terminal width (`COLUMNS=40` and `COLUMNS=200` change
  it), as curl's `print_category` narrows it to fit `get_terminal_columns`.
- `-M` prints the built-in manual: 7849 lines, 299744 bytes with CR LF line ends, the same at any
  width.
- `--help <option>` prints that option's manual section; `-h` / `-M` end parsing as `-V` does; a
  `help` line in a `-K` file prints the usage page and carries on.

`CommandLineOptionTable` holds only the options Curl parses today, not curl's 274, and carries no
descriptions or categories.

## Decision

1. **The help rows are curl's own table, not ours.** `CurlHelpTable` is a copy of curl 8.21.0's
   generated `src/tool_listhelp.c` (option as shown, description, categories), and
   `CurlHelpCategories` mirrors its `CURLHELP_*` bits. `CommandLineOptionTable` cannot generate the
   pages: curl's help lists every option, including those this build does not implement, and a
   drop-in replacement must print the same page. This is the "where possible" limit the task
   allows; the text is generated from a table, just curl's.
2. **The layout is ported, not stored.** `CurlHelpText` ports `tool_help`, `print_category`,
   `get_categories` and `get_categories_list`, taking the terminal width as a parameter, so every
   width curl can produce is produced. The console passes `TerminalColumns.Resolve()`.
3. **The manual is stored, not generated.** `CurlManual` reads the measured `-M` output, kept
   with LF line ends as the embedded resource `CurlManual.txt` (292 KB). Generating it would mean
   porting curl's man-page renderer for one fixed text. An embedded resource is AOT-safe and needs
   no package. Its SHA-256 over CR LF lines is pinned in a test.
4. **Parsing.** `-M` / `--manual` is a negatable flag; `-h` / `--help` is a new row kind,
   `CommandLineOption.Subject`, that reads the attached value or else the next argument as its
   subject and, inside a bundle, counts only as the last letter (`-hv` is read as nothing, as
   curl reads it). Both end parsing like `-V`; the result is
   `CommandLineParseResult.InformationRequested`.
5. **Split into follow-ups.** `--help <option>` needs curl's full option list with letters and
   negatability (BL-374); the `-K` file behaviour of `help` needs a print-and-carry-on result
   (BL-375, ignored until then, as `manual` and `version` are by curl); writing the pages from
   `Curl.Console` is BL-376, because `Curl.Console` was being changed by another lane.

## Consequences

- A new curl version means refreshing `CurlHelpTable.cs`, `CurlManual.txt` and the
  `Curl.Cli.UnitTests/HelpReference` files from the new build, not editing text by hand.
- Since BL-376, `CurlCommandRunner` writes `-h`, `--help <subject>` (an option's manual section
  included) and `-M` to standard output with the platform newline, at the width
  `TerminalColumns.Resolve()` gives, and exits 0.
