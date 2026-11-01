using System.Text.Json;

namespace Curl.Extended.Protocol.SignalR.Json;

/// <summary>One JSON value as received: its token type, its JSON text as sent, and its text when it is a string.</summary>
internal sealed record JsonField(JsonTokenType TokenType, string RawJson, string? Text);
