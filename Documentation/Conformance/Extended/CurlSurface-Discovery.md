# CurlSurface: discovery of the extended protocols

How a user or an agent finds Curl's extended protocols, and how `CURL_EXTENSIONS=0` hides them, checked
through the real hosts in process: `Curl.Console` through `InProcessCurl` and `Curl.Mcp` through
`CurlMcpHost.CreateRegistry` with the real extension handlers. The rules come from the root `CLAUDE.md`
("Extensions"), ADR-0999, ADR-1006, ADR-1008, ADR-1142, ADR-1180 and ADR-1181. "Every scheme" is every
scheme `curl --extended-help` lists; the extended protocol libraries' own schemes are pinned in
`DiscoveryCaseRun.ExtendedProtocols`, so a scheme that drops out of the listing fails `Off-2` and
`Listing-1`. The Plugin extension has no fixed scheme; its helper schemes are BL-4489's. IDs are
`CurlSurface:<rule>-<n>`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:Off-1 | MUST | With `CURL_EXTENSIONS=0` a transfer to every extended scheme exits 1 with standard error byte for byte the one curl writes for a scheme nobody serves (`foo://x/`), the scheme's name in place of `foo`, and writes nothing to standard output. | ExtensionsOffCases.EveryExtendedScheme_WithExtensionsOff_IsRefusedAsAnUnknownSchemeIs |
| CurlSurface:Off-2 | MUST | Every scheme of every extended protocol library is one `--extended-help` lists with extensions on, so the other checks reach it. | ExtensionsOffCases.EveryExtendedProtocolScheme_IsListedWithExtensionsOn |
| CurlSurface:Off-3 | MUST | With `CURL_EXTENSIONS=0` every extension's long option is refused as curl refuses an unknown option: exit 2 and the same standard error. | ExtensionsOffCases.EveryExtensionOption_WithExtensionsOff_IsRefusedAsAnUnknownOptionIs |
| CurlSurface:Listing-1 | MUST | With extensions on, `--extended-help` prints one line per extended protocol naming its schemes. | ExtensionListingCases.ExtendedHelp_WithExtensionsOn_ListsEveryExtendedProtocolWithItsSchemes |
| CurlSurface:Listing-2 | MUST | With `CURL_EXTENSIONS=0`, `--extended-help` is refused as an unknown option, exit 2 (BL-4937). | ExtensionListingCases.ExtendedHelp_WithExtensionsOff_IsRefusedAsAnUnknownOption |
| CurlSurface:Listing-3 | MUST | With extensions on, `--ai-help extensions` has a section for every extension `--extended-help` lists, naming its schemes. | ExtensionListingCases.AiHelpExtensions_WithExtensionsOn_HasASectionForEveryExtensionWithItsSchemes |
| CurlSurface:Listing-4 | MUST | With `CURL_EXTENSIONS=0`, `--ai-help` is refused as an unknown option, exit 2 (BL-4937). | ExtensionListingCases.AiHelpExtensions_WithExtensionsOff_IsRefusedAsAnUnknownOption |
| CurlSurface:Listing-5 | MUST NOT | `curl -V` names no extended scheme: its output, `Protocols:` and `Features:` included, is the same with extensions on as off. | ExtensionListingCases.Version_WithExtensionsOn_IsCurlsOwnAndNamesNoExtendedScheme |
| CurlSurface:Listing-6 | MUST | The Plugin extension's `--extended-help` line lists no fixed scheme (`Schemes: none.`). | ExtensionListingCases.ExtendedHelp_PluginProtocolLine_ListsNoFixedScheme |
| CurlSurface:Mcp-1 | MUST | With extensions on, `Curl.Mcp`'s `parse_url` and `build_url` accept every extended scheme, in lower and upper case. | McpDiscoveryCases.ParseAndBuildUrl_WithExtensionsOn_AcceptEveryExtendedSchemeInAnyCase |
| CurlSurface:Mcp-2 | MUST | With `CURL_EXTENSIONS=0`, `parse_url` and `build_url` refuse every extended scheme as unsupported (`CURLUE_UNSUPPORTED_SCHEME`). | McpDiscoveryCases.ParseAndBuildUrl_WithExtensionsOff_RefuseEveryExtendedSchemeAsUnsupported |
| CurlSurface:Mcp-3 | MUST | With extensions on, `version`'s `extendedProtocols` lists every extended scheme once in `--extended-help` order, and `protocols` stays curl's list, the same as with extensions off and naming no extended scheme. | McpDiscoveryCases.Version_WithExtensionsOn_ListsEveryExtendedSchemeOnceInListingOrderApartFromCurlsProtocols |
| CurlSurface:Mcp-4 | MUST | With `CURL_EXTENSIONS=0`, `version`'s `extendedProtocols` is empty. | McpDiscoveryCases.Version_WithExtensionsOff_HasNoExtendedProtocols |
