namespace Curl.Extension.WireGuard.Configuration;

/// <summary>
/// Thrown when a wg-quick configuration file cannot be read: its message names the line at fault
/// ("line 3: ...") and <see cref="LineNumber" /> holds it.
/// </summary>
public sealed class WireGuardConfigurationException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="WireGuardConfigurationException" /> class.</summary>
    /// <param name="lineNumber">The one-based line the problem is on.</param>
    /// <param name="problem">What is wrong with that line.</param>
    public WireGuardConfigurationException(int lineNumber, string problem)
        : base($"line {lineNumber}: {problem}")
    {
        LineNumber = lineNumber;
    }

    /// <summary>Gets the one-based line the problem is on.</summary>
    public int LineNumber { get; }
}
