---
id: BL-911
title: Print a Markdown option reference for AI agents under --ai-help, a short index by default and one category on request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-911 — Print a Markdown option reference for AI agents under --ai-help, a short index by default and one category on request

## Goal

`curl --ai-help` prints a short Markdown index an AI agent can read cheaply, and `curl --ai-help <category>` (or `all`) prints the Markdown reference for that category's options, generated from the same tables `--help` and `--manual` use so the two never disagree.

## Context

Stewart asked for this on 2026-09-29: `--help` is for people, `--ai-help` is for AI agents, and its output is Markdown.

Today `--help`/`-h` prints curl's help byte for byte (`Curl.Cli.UnitLibrary/CurlHelpText.cs`, `CurlHelpTable.cs`, `CurlHelpCategories.cs`) and `--manual`/`-M` prints `CurlManual.txt` (`CurlManual.cs`), with `CurlOptionManualSection.cs` able to cut one option's section out of it. `Curl.Console/CurlCommandRunner.cs` picks the output near line 3583 (`ManualRequested`, `HelpRequested`).

Decisions already made with Stewart:
- The option is `--ai-help`, two dashes. A single-dash `-aihelp` cannot work: curl reads it as the bundled short options `-a -i -h -e -l -p`.
- No argument prints a **short index**, not the full reference: an agent's context window is the scarce resource and the full reference covers about 250 options.

Real curl has no `--ai-help` and rejects it with exit 2, so this is a deliberate, additive departure from drop-in behaviour. No working script can depend on the rejection. Record that in an ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] `--ai-help` is a recognised long option (with an optional category argument, like `--help`) in `CommandLineOptionTable`, and exits 0.
- [ ] `curl --ai-help` prints Markdown: a one-paragraph statement of what this curl is, the list of categories (each with its one-line description from `CurlHelpCategories` and the exact command to expand it), the 20 most-used options with their short alias, argument and one-line summary, and a line telling the agent to run `curl --ai-help <category>` or `curl --ai-help all` for more.
- [ ] `curl --ai-help <category>` prints a `#` heading for the category and, for each of its options, a `##` heading with the long name, then its short alias, argument placeholder, `--no-` form if any, whether it may be repeated, and the description from that option's section of `CurlManual.txt`.
- [ ] `curl --ai-help all` prints every category in that form, plus a section of exit codes with their meanings and a section of `--write-out` variables.
- [ ] An unknown category prints the same error and exit code `--help <unknown>` does.
- [ ] Every option in `CommandLineOptionTable` appears under at least one category in `--ai-help all`; a unit test fails when an option is added without a place there.
- [ ] The output is valid Markdown with no terminal-width wrapping, and is identical on Windows, Linux and macOS.
- [ ] `--help` and `--manual` output is unchanged byte for byte.
- [ ] An ADR records the `--ai-help` departure from real curl and the index-by-default choice.
- [ ] `Curl.Cli.UnitLibrary` and `Curl.Console` keep 100% line and branch coverage.

## Notes

From now on any task that adds or changes a command-line option also keeps `--ai-help` right; the unit test in the acceptance criteria enforces the listing, not the wording.

## Log

- 2026-09-29: Created.
