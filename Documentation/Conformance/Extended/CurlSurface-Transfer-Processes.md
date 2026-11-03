# CurlSurface: transfer options for the schemes that start a program

curl's generic transfer options (`-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body`, `-m`) for the two extended
schemes that start a program instead of opening a connection: `mcp+stdio` (the MCP client's stdio transport,
ADR-1031) and a `curl-proto-<scheme>` helper scheme (the Plugin extension, ADR-1142; the cases use `demo://`
served by `curl-proto-demo`). Curl runs in process through `ExtendedConformanceHarness.RunAsync` with a stand-in
`IExtensionProcessStarter` (ADR-1203), so no real process starts, and on an injected `TimeProvider` that moves only
when a case advances it. The cases are in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Processes/`, one
file per scheme.

Expected values come from curl 8.22.0's manual page (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail",
"--fail-with-body", "-m", "EXIT CODES") and these decisions:

- Neither scheme opens a connection, so `-v` writes no connection lines, as curl writes none for a transfer that
  connects nowhere (`file://`), and neither scheme uses TLS.
- With no connection, `%{remote_ip}` is empty and `%{remote_port}` is `-1`: ADR-0119, point 3, curl's answer for a
  transfer that made no connection.
- `--fail` and `--fail-with-body` act on HTTP response codes of 400 and up (manual, "--fail"). Neither library has
  a response code: an MCP server that fails ends with exit 8 and a helper's `error` reply with exit 56 (ADR-1142).
  So no server error maps to exit 22, and both options leave the transfer's own exit code and output alone.
- `-m` ends the transfer with exit 28 at the deadline, on the injected clock, while the program never answers.
- `%{time_total}` is `0.000000` on a clock the case never advances.

IDs are `CurlSurface:<option>-<scheme>-<n>`, the scheme part naming `McpStdio` or `CurlProto`, so they cannot clash
with the other transfer parts' IDs.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:Verbose-McpStdio-1 | MUST | `-v` on `mcp+stdio` writes no connection lines to standard error and leaves standard output as it is without `-v`. | McpStdioTransferCases.Verbose_NoConnectionLinesAndStandardOutputUnchanged |
| CurlSurface:Verbose-McpStdio-2 | MUST | `-v` writes TLS lines for a TLS scheme. | n/a: `mcp+stdio` talks to a program over its standard input and output; there is no TLS. |
| CurlSurface:Output-McpStdio-1 | MUST | `-o file` writes what standard output would hold to the file, in a temporary directory, and nothing to standard output. | McpStdioTransferCases.OutputFile_HoldsWhatStandardOutputWouldAndStandardOutputIsEmpty |
| CurlSurface:RemoteName-McpStdio-1 | MUST | `-O` writes the file named after the URL's last path segment (`reference-server`), here under `--output-dir`. | McpStdioTransferCases.RemoteName_WritesTheFileNamedAfterTheLastPathSegment |
| CurlSurface:WriteOut-McpStdio-1 | MUST | After a transfer that succeeds, `-w` prints `%{exitcode}` 0, an empty `%{errormsg}`, the URL as `%{url}` and `%{url_effective}`, `mcp+stdio` as `%{scheme}`, an empty `%{remote_ip}`, `-1` as `%{remote_port}`, the bytes written as `%{size_download}` and `0.000000` as `%{time_total}`. | McpStdioTransferCases.WriteOut_SuccessfulTransfer_PrintsTheUrlSchemeSizeAndNoConnection |
| CurlSurface:WriteOut-McpStdio-2 | MUST | After a transfer that fails, `%{exitcode}` and `%{errormsg}` are the exit code and the message curl writes to standard error. | McpStdioTransferCases.WriteOut_FailedTransfer_PrintsTheExitCodeAndErrorMessage |
| CurlSurface:Fail-McpStdio-1 | MUST | `--fail` and `--fail-with-body` leave exit 0 and the output of a transfer that succeeds, and exit 8 of a server that fails: no MCP answer maps to exit 22. | McpStdioTransferCases.Fail_LeavesTheTransfersOwnExitCodeAndOutput |
| CurlSurface:MaxTime-McpStdio-1 | MUST | `-m` against a server that never answers ends with exit 28 at the deadline on the injected clock, with nothing on standard output. | McpStdioTransferCases.MaxTime_ServerNeverAnswers_Exit28AtTheDeadline |
| CurlSurface:Verbose-CurlProto-1 | MUST | `-v` on a `curl-proto-<scheme>` scheme writes no connection lines to standard error and leaves standard output as it is without `-v`. | CurlProtoTransferCases.Verbose_NoConnectionLinesAndStandardOutputUnchanged |
| CurlSurface:Verbose-CurlProto-2 | MUST | `-v` writes TLS lines for a TLS scheme. | n/a: a helper talks over its standard input and output; Curl has no TLS for it (ADR-1142). |
| CurlSurface:Output-CurlProto-1 | MUST | `-o file` writes the helper's body to the file, in a temporary directory, and nothing to standard output. | CurlProtoTransferCases.OutputFile_HoldsWhatStandardOutputWouldAndStandardOutputIsEmpty |
| CurlSurface:RemoteName-CurlProto-1 | MUST | `-O` writes the file named after the URL's last path segment (`item`), here under `--output-dir`. | CurlProtoTransferCases.RemoteName_WritesTheFileNamedAfterTheLastPathSegment |
| CurlSurface:WriteOut-CurlProto-1 | MUST | After a transfer that succeeds, `-w` prints `%{exitcode}` 0, an empty `%{errormsg}`, the URL as `%{url}` and `%{url_effective}`, the scheme (`demo`) as `%{scheme}`, an empty `%{remote_ip}`, `-1` as `%{remote_port}`, the body's bytes as `%{size_download}` and `0.000000` as `%{time_total}`. | CurlProtoTransferCases.WriteOut_SuccessfulTransfer_PrintsTheUrlSchemeSizeAndNoConnection |
| CurlSurface:WriteOut-CurlProto-2 | MUST | After a helper's `error` reply, `%{exitcode}` is 56 and `%{errormsg}` the message curl writes to standard error. | CurlProtoTransferCases.WriteOut_FailedTransfer_PrintsTheExitCodeAndErrorMessage |
| CurlSurface:Fail-CurlProto-1 | MUST | `--fail` and `--fail-with-body` leave exit 0 and the body of a transfer that succeeds, and exit 56 of an `error` reply: no helper reply maps to exit 22 (ADR-1142). | CurlProtoTransferCases.Fail_LeavesTheTransfersOwnExitCodeAndOutput |
| CurlSurface:MaxTime-CurlProto-1 | MUST | `-m` against a helper that never answers ends with exit 28 at the deadline on the injected clock, with nothing on standard output. | CurlProtoTransferCases.MaxTime_HelperNeverAnswers_Exit28AtTheDeadline |
