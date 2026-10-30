namespace Curl.Extension.Abstractions;

/// <summary>What an extension's command-line option takes after its name.</summary>
public enum ExtensionOptionArgument
{
    /// <summary>Nothing: the option is a flag, given or not (<c>--sse</c>).</summary>
    None,

    /// <summary>One value, attached (<c>--cache-dir=dir</c>) or the next argument (<c>--cache-dir dir</c>).</summary>
    Value,
}
