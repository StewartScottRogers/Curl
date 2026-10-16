# ADR-0600: The MCP checker reads a `-w @file` template and checks its `%output{}` files

- Status: Accepted
- Date: 2026-10-16
- Task: BL-2460
- Decided by Claude under Stewart's delegation.

## Context

ADR-0564 decision 7 left the contents of a `-w @file` template unread: `McpArgumentChecker`
checked that the template file lies inside `CURL_MCP_ROOT` but not what it says. curl reads the
file as the template, so a template holding `%output{/outside/x}` writes a file outside the root.
An agent can create such a template inside the root with `-o` and then run `-w @template`. Two
ways to close it: read the template, as a `-K` file is read, and scan it for `%output{}` as an
inline template is scanned; or refuse every `-w @file` whose file contains `%output{`.

## Decision

1. The checker reads the template. `-w @file` (and `--write-out`, and `--expand-write-out` without
   `{{`) must name a file inside the root that can be read; its text then goes through the same
   `%output{file}` / `%output{>>file}` scan an inline template gets (`McpOptionValuePaths.TemplatePaths`),
   and every file it names must lie inside the root. A refusal names the option and starts
   `In write-out template '<file>':` when a directive inside it is the cause.
2. A template that cannot be read (missing, a folder, no permission) is refused, as a `-K` file
   is, since its directives cannot be checked.
3. `-w @-` is refused, as `-K -` is: the `curl` tool passes its caller's `standardInput` to curl
   unchecked, so a template read from it could hold `%output{}` to any file. ADR-0564 allowed it.

Reading was chosen over refusing every `%output{` because it matches how inline templates and
`-K` files are already treated, and it keeps `%output{}` to a file inside the root working;
the cost is one file read, which `-K` already pays. This supersedes ADR-0564 decision 7.

## Consequences

The checker now reads two kinds of file: `-K` configs and `-w @file` templates. Between the
check and curl's run the template could change; the same holds for `-K` files and for any path
checked before curl runs, and the `curl` tool runs straight after the check.
