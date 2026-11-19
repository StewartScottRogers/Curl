# ADR-1310: The curl-mcp `curl` tool knows the extension options and confines those that name a path

- Status: Accepted
- Date: 2026-11-19
- Task: BL-5564
- Decided by Claude under Stewart's delegation.

## Context

`McpArgumentChecker` knew only curl's own options, so `--dns-json` and `--dns-transport` were refused as unknown (bug report 2026-11-19T0930, finding 1), although the console runs them. An extension option carries no "names a path" flag, and `Curl.Mcp.UnitLibrary` cannot reference the extension registry.

## Decision

The host (`Curl.Mcp`) reads the registered options from `ExtensionCommandLineOptions.For` (none under `CURL_EXTENSIONS=0`) and passes them to `McpCurlTool` and `McpArgumentChecker` as `McpExtensionOption` (name, takes a value). `McpExtensionOptionPathTable` classifies each as a path option or a pathless one; a value option in neither is confined as a path like `-o`, so a new option fails closed. A drift test walks the registry and fails, naming any option the table does not classify. Adding a flag to `ExtensionOption` and editing about 25 extension projects was left out as disproportionate; the table lives with `McpOptionPathTable`, the original options' table, instead.

## Consequences

An extension option that is new needs one line in the table to be usable with a pathless value; until then its value is path-checked, never silently unchecked.
