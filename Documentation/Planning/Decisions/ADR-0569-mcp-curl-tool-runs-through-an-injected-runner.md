# ADR-0569: The MCP `curl` tool runs through an injected `ICurlCommandRunner`

- Status: Accepted
- Date: 2026-10-15
- Decided by Claude under Stewart's delegation (BL-2381)

## Context

ADR-0548 puts all tool logic in `Curl.Mcp.UnitLibrary`, which must not reference the
`Curl.Console` executable whose `InProcessCurl` runs a command line. The `curl` tool must check
its argument array against `CURL_MCP_ROOT` (ADR-0564) before anything runs, never change the
server's process-wide current directory, and return curl's standard output, standard error and
exit code within `CURL_MCP_MAX_RESPONSE_BYTES` (ADR-0555).

## Decision

- `ICurlCommandRunner.RunAsync(arguments, standardInput, standardOutput, standardError,
  workingDirectory, cancellationToken)` gives the exit code. `Curl.Mcp` adapts
  `InProcessCurl` to it (the next task of `Documentation/Planning/Curl-Mcp.md`).
- `McpCurlTool.RunAsync(arguments, standardInput, token)` runs `McpArgumentChecker` first; a
  refusal returns `McpToolFailure.PathRefused` naming the option and never calls the runner.
  Then the runner gets the arguments unchanged (no program name stripped or added, so a leading
  `curl` is a URL, as curl reads it), the resolved root as working directory (`null` when no
  root is set: the checker has already refused every file path), standard input as the UTF-8
  bytes of the given text or empty, and the request token.
- Standard output and standard error are each captured in an `McpCappedCaptureStream`, which
  keeps the cap plus one byte (so a cut can back off a split UTF-8 character) and counts every
  byte, so memory stays bounded while the truncation note still states the whole size.
  `McpReturnedBytes.Encode(kept, totalBytes, cap)` encodes that kept start.
- The result is `McpCurlToolResult`: exit code, both outputs and their truncation flags, and a
  tool error for a nonzero exit (`McpToolFailure.FromCurlExit` with curl's standard error text;
  binary standard error leaves only the exit code line). Unlike `McpToolResult` it carries
  output and error together, because curl's standard output and standard error are both useful
  when it fails. A request cancelled before or during the run returns `McpToolFailure.Cancelled`
  with no output.

## Consequences

The tool is unit tested with a fake runner and no network. `--help` and `--ai-help` output
passes through as curl wrote it, cut only by the cap.
