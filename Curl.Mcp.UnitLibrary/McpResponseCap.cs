using System.Globalization;

namespace Curl.Mcp;

/// <summary>
/// The most bytes of output a tool returns, set by <c>CURL_MCP_MAX_RESPONSE_BYTES</c>
/// (ADR-0548). Output past it is cut and the cut is stated, never silent.
/// </summary>
public sealed record McpResponseCap
{
    /// <summary>The environment variable that sets the cap.</summary>
    public const string VariableName = "CURL_MCP_MAX_RESPONSE_BYTES";

    /// <summary>The cap when the variable is unset or empty: 1 MiB.</summary>
    public const int DefaultMaxBytes = 1024 * 1024;

    /// <summary>Creates a cap of <paramref name="maxBytes" /> bytes.</summary>
    /// <param name="maxBytes">The most bytes a tool returns; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes" /> is zero or negative.</exception>
    public McpResponseCap(int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        MaxBytes = maxBytes;
    }

    /// <summary>The most bytes of output a tool returns.</summary>
    public int MaxBytes { get; }

    /// <summary>
    /// Reads the cap from <c>CURL_MCP_MAX_RESPONSE_BYTES</c>: the default when it is unset or
    /// empty, otherwise a positive whole number of bytes in plain digits.
    /// </summary>
    /// <param name="readEnvironmentVariable">Gives an environment variable's value, or <see langword="null" /> when it is unset.</param>
    /// <returns>The cap.</returns>
    /// <exception cref="McpConfigurationException">The value is not a positive whole number of bytes, so the server must not start.</exception>
    public static McpResponseCap FromEnvironment(Func<string, string?> readEnvironmentVariable)
    {
        string? value = readEnvironmentVariable(VariableName);
        if (string.IsNullOrEmpty(value))
        {
            return new McpResponseCap(DefaultMaxBytes);
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int maxBytes) || maxBytes == 0)
        {
            throw new McpConfigurationException(
                $"{VariableName} must be a positive whole number of bytes; got '{value}'.");
        }

        return new McpResponseCap(maxBytes);
    }
}
