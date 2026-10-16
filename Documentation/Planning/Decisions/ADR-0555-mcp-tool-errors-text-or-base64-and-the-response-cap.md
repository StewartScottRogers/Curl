# ADR-0555 — MCP tool errors carry curl's message and the code; output is text or base64, capped in raw bytes at a character boundary

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2380.

## Context

ADR-0548 and `Documentation/Planning/Curl-Mcp.md` say a curl failure is an MCP tool error carrying
curl's own message and exit code, output that is not UTF-8 is base64, and what a tool returns is
capped by `CURL_MCP_MAX_RESPONSE_BYTES` (default 1 MiB) with truncation stated. They leave open the
error text's shape, what the cap counts, how text is cut, and which values of the variable start
the server.

## Decision

1. `Curl.Mcp.UnitLibrary`'s `McpToolFailure.FromCurlExit` gives no error for exit 0. For any other
   code its text is curl's message as written to standard error, trimmed, then a line
   `curl exit code <n> (<name>)`, where the name is `Curl.Api.Codes.CurlCode`'s member for the
   number (it keeps every number, the obsolete ones too) or the number again for one it does not
   name. With no message the code line stands alone. A refused path is
   `Refused <option>: <reason>`; a cancelled request is its own error, `McpToolFailure.Cancelled`.
2. The cap counts the output's raw bytes, not the encoded result, so a client's limit means the
   same bytes whether they come back as text or base64. `McpReturnedBytes.Encode` cuts valid
   UTF-8 before a character the cap would split (backing off at most three continuation bytes),
   so text never ends in a broken sequence; output that is not valid UTF-8 is cut at the cap
   exactly and returned as base64. A cut always carries
   `Output truncated: returned <r> of <t> bytes (CURL_MCP_MAX_RESPONSE_BYTES caps it).`
3. `McpResponseCap.FromEnvironment` takes the default when the variable is unset or empty, and
   otherwise accepts plain ASCII digits giving 1 to `int.MaxValue`. Anything else - a sign,
   spaces, a decimal point, zero, an overflow - throws `McpConfigurationException` naming the
   variable and the value, so the server fails to start rather than guess.
4. `McpToolRunner.RunAsync` passes the MCP request's token to the run, never starts a run whose
   token is already cancelled, and reports a run cancelled while it ran - whether it threw or
   returned partial output - as the cancelled error with no output.

## Consequences

The `curl`, `transfer`, `parse_url`, `build_url` and `version` tools (BL-2381 onwards) share one
result shape. Empty counts as unset because a variable set to nothing is how most shells and MCP
client configurations leave it out.
