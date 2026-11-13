# McpClient: MCP 2025-06-18 lifecycle, tools and Streamable HTTP

Curl's `mcp://` and `mcps://` client (`Curl.Extended.Protocol.McpClient.UnitLibrary`) measured against the Model
Context Protocol, revision 2025-06-18 (modelcontextprotocol.io/specification/2025-06-18): "Base Protocol > Overview",
"Lifecycle", "Transports > Streamable HTTP", "Server Features > Tools" and "Utilities > Pagination" (BL-4482). The
stdio transport is in [McpClient-Stdio.md](McpClient-Stdio.md) (BL-4483). Only requirements that bind a client are
rows; the server's own are kept by the reference server in `Curl.Conformance.Extended.UnitLibrary/McpClient/`, whose
JSON-RPC side (`McpReferenceToolServer`) is apart from its transport (`McpStreamableHttpReferenceServer`). Cases
listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` fail today and name the task that fixes them.
ADR-1190 records what Curl does where the specification leaves the client's reaction open.

| ID | Level | Requirement | Covered by |
|----|:-----:|-------------|------------|
| McpClient:MCP20250618-Base-1 | MUST | A request includes a string or integer id. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Base-2 | MUST NOT | A request id is not null; a notification includes no id. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Base-3 | MUST NOT | A request id has not been used before by the requestor within the same session. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Base-4 | MUST | A JSON-RPC error response is understood as the request's failure (an error has an integer code and a message). | McpToolsCases.JsonRpcError_ReportsCodeAndMessageAndExits8 |
| McpClient:MCP20250618-Base-5 | MUST | Responses include the request's id and exactly one of result and error. | n/a: Curl sends no responses; it answers no server request |
| McpClient:MCP20250618-Lifecycle-1 | MUST | Initialization is the first interaction between client and server. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Lifecycle-2 | MUST | The client sends `initialize` with the protocol version it supports, its capabilities and its implementation information. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Lifecycle-3 | MUST | After successful initialization the client sends `notifications/initialized`. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Lifecycle-4 | SHOULD NOT | The client sends no request other than pings before the server has answered `initialize`. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Lifecycle-5 | MUST / SHOULD | The version in `initialize` is one the client supports, and should be the latest it supports. | McpLifecycleCases.Initialize_ThenInitializedNotification_BeforeAnyOtherRequest |
| McpClient:MCP20250618-Lifecycle-6 | SHOULD | If the client does not support the version in the server's response, it disconnects. | McpLifecycleCases.ServerAnswersAVersionCurlDoesNotSupport_CurlDisconnectsWithExit8 |
| McpClient:MCP20250618-Lifecycle-7 | MUST | Both parties use only capabilities that were successfully negotiated. | McpLifecycleCases.ServerDeclaresNoToolsCapability_CurlSendsNoToolsRequestAndExits8 |
| McpClient:MCP20250618-Lifecycle-8 | SHOULD | Implementations establish timeouts for all sent requests and issue a cancellation notification when one expires. | n/a: curl's `-m` bounds the whole session and ends it; no later message can be sent, and the harness's own timeout test covers `-m` |
| McpClient:MCP20250618-StreamableHttp-1 | MUST | The client uses HTTP POST to send each JSON-RPC message to the MCP endpoint. | McpStreamableHttpCases.JsonReply_EveryMessageIsOnePostAcceptingJsonAndEventStream |
| McpClient:MCP20250618-StreamableHttp-2 | MUST | The POST includes an Accept header listing both `application/json` and `text/event-stream`. | McpStreamableHttpCases.JsonReply_EveryMessageIsOnePostAcceptingJsonAndEventStream |
| McpClient:MCP20250618-StreamableHttp-3 | MUST | The POST body is a single JSON-RPC request, notification or response. | McpStreamableHttpCases.JsonReply_EveryMessageIsOnePostAcceptingJsonAndEventStream |
| McpClient:MCP20250618-StreamableHttp-4 | MUST | The client supports both an `application/json` reply and a `text/event-stream` reply. | McpStreamableHttpCases.EventStreamReply_SkipsUnrelatedMessagesBeforeTheResponse |
| McpClient:MCP20250618-StreamableHttp-5 | MUST | With an `Mcp-Session-Id` from initialization, the client includes it on all subsequent HTTP requests. | McpStreamableHttpCases.LaterRequests_CarrySessionIdAndNegotiatedVersion_AndTheSessionEndsWithDelete |
| McpClient:MCP20250618-StreamableHttp-6 | MUST | The client includes `MCP-Protocol-Version` on all subsequent HTTP requests. | McpStreamableHttpCases.LaterRequests_CarrySessionIdAndNegotiatedVersion_AndTheSessionEndsWithDelete |
| McpClient:MCP20250618-StreamableHttp-7 | SHOULD | The version sent in `MCP-Protocol-Version` is the one negotiated during initialization. | McpStreamableHttpCases.LaterRequests_CarrySessionIdAndNegotiatedVersion_AndTheSessionEndsWithDelete |
| McpClient:MCP20250618-StreamableHttp-8 | MUST | On a 404 to a request carrying `Mcp-Session-Id`, the client starts a new session with a new `initialize` without a session id. | McpStreamableHttpCases.ExpiredSession404_CurlStartsANewSessionWithoutTheOldId |
| McpClient:MCP20250618-StreamableHttp-9 | SHOULD | A client that no longer needs a session sends HTTP DELETE with its `Mcp-Session-Id`. | McpStreamableHttpCases.LaterRequests_CarrySessionIdAndNegotiatedVersion_AndTheSessionEndsWithDelete |
| McpClient:MCP20250618-StreamableHttp-10 | MUST | The transport runs over HTTPS as over HTTP (`mcps://`). | McpStreamableHttpCases.Mcps_RunsTheSameExchangeOverTls |
| McpClient:MCP20250618-StreamableHttp-11 | SHOULD | A client that wishes to resume after a disconnection issues GET with `Last-Event-ID`. | n/a: Curl does not resume a broken stream; it ends the transfer, as curl does a broken HTTP transfer |
| McpClient:MCP20250618-Tools-1 | MUST | `tools/list` lists the tools; the client follows the result's pagination. | McpToolsCases.PaginatedList_FollowsEachOpaqueCursorUntilTheLastPage |
| McpClient:MCP20250618-Tools-2 | MUST | `tools/call` sends the tool's name and arguments; the result's content is text, image, audio, resource link or embedded resource. | McpToolsCases.CallReturningTextImageAndResource_WritesEachContentItem |
| McpClient:MCP20250618-Tools-3 | MUST | A tool execution error is reported in the result with `isError` true, apart from a protocol error. | McpToolsCases.ToolReportsIsError_WritesItsContentAndExits22 |
| McpClient:MCP20250618-Tools-4 | SHOULD | Clients prompt for confirmation of sensitive operations, show tool inputs, validate results, time out calls and log tool usage. | n/a: curl calls only the tool its user names on the command line, with the arguments given there, and passes the result to no model |
| McpClient:MCP20250618-Tools-5 | MUST | Clients consider tool annotations untrusted unless from a trusted server. | n/a: Curl reads no tool annotations |
| McpClient:MCP20250618-Tools-6 | SHOULD | Clients validate structured results against the tool's output schema. | n/a: Curl writes the content items and never reads `structuredContent` |
| McpClient:MCP20250618-Pagination-1 | MUST | Clients treat cursors as opaque tokens: never parse or modify them, never persist them across sessions. | McpToolsCases.PaginatedList_FollowsEachOpaqueCursorUntilTheLastPage |
| McpClient:MCP20250618-Authorization-1 | MUST | The client uses the Authorization request header with the Bearer scheme on every HTTP request to the MCP server, including requests in the same session (`--oauth2-bearer`, or `-H "Authorization: Bearer ..."`). | McpAuthorizationCases.BearerToken_EveryRequestCarriesItInTheAuthorizationHeader |
| McpClient:MCP20250618-Authorization-2 | MUST NOT | Access tokens are not sent in the URI query string. | McpAuthorizationCases.BearerToken_EveryRequestCarriesItInTheAuthorizationHeader |
| McpClient:MCP20250618-Authorization-3 | MUST | A 401 means authorization is required or the token is invalid; Curl ends with exit 67, as an authentication failure (ADR-1243). | McpAuthorizationCases.WithoutAToken_The401IsExit67 |
| McpClient:MCP20250618-Authorization-4 | SHOULD | Clients use OAuth 2.0 Protected Resource Metadata and the authorization server's metadata to obtain a token. | n/a: curl obtains no token by itself on HTTP; its user passes one with `--oauth2-bearer` (ADR-1243) |
| McpClient:Curl-ConnectionReset-1 | MUST | A connection reset during an `mcp://` or `mcps://` Streamable HTTP exchange ends with exit 56 and `Recv failure: Connection was reset`, or exit 55 and `Send failure: Connection was reset` when it happens while sending, as every other extension scheme does (BL-5029). | n/a: covered by McpStreamableHttpTransportTests.SendRequestAsync_ConnectionResetWhileReading_FailsWithExit56 and SendRequestAsync_ConnectionResetWhileWriting_FailsWithExit55 in Curl.Extended.Protocol.McpClient.UnitTests, not a conformance case (the reference server cannot reset a connection) |
