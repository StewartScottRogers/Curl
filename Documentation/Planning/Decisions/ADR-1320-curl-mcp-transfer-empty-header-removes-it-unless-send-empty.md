# ADR-1320: The curl-mcp transfer tool removes a header given an empty value, unless sendEmpty is set

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5598
- Decided by Claude under Stewart's delegation.

## Context

The `transfer` tool turned a request header with an empty value into the `CURLOPT_HTTPHEADER` line `Name;`, which libcurl sends as an empty header. An agent writing `{"name":"Accept","value":""}` expects what `curl -H "Accept:"` does: the header, default or not, is removed. curl sends an empty header only for `-H "Name;"`, so the tool needs a way to ask for that too.

## Decision

An empty (or all white space) value becomes `Name:`, which removes the header as `curl -H "Name:"` does. A header object may carry an optional `sendEmpty` boolean, default `false`; with `true` and an empty value the line is `Name;`, sent empty as `curl -H "Name;"` sends it. A non-empty value is sent as given whatever `sendEmpty` says. `McpTransferHeader` takes `SendEmpty` as an optional last parameter, ignored when written while `false`, so response headers serialise as before. The tool description says both.

A flag was chosen over a sentinel value (such as `null` or a `;` suffix in the name) because it is explicit in the input schema an agent reads, and keeps `name` and `value` plain strings.

## Consequences

A caller that relied on an empty value being sent empty now gets the header removed, matching curl; it sets `sendEmpty` to keep the old behaviour. The golden `tools/list` answers carry the new description and property.
