using System.Text;

namespace Curl.Cli;

/// <summary>
/// Expands the <c>{{name}}</c> and <c>{{name:function...}}</c> references in the value of an option given
/// as <c>--expand-&lt;option&gt;</c>, as curl 8.21.0's <c>varexpand</c> does: each reference becomes the
/// named <c>--variable</c>'s bytes, after its functions (<see cref="VariableFunctions"/>), or nothing when
/// no variable has that name; <c>\{{</c> becomes <c>{{</c>. A reference whose name is empty, 128 bytes or
/// longer, or holds anything but letters, digits and underscores is kept as written, with a warning; a
/// <c>{{</c> with no <c>}}</c> after it ends the expansion there, with a warning. When no reference was
/// replaced the value is used exactly as given, <c>\{{</c> included.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-09-27 through <c>--expand-data</c> against a loopback
/// server, and checked against <c>src/var.c</c> at tag <c>curl-8_21_0</c>. The value is expanded as
/// UTF-8 bytes and the result read back as UTF-8, so bytes that are not UTF-8 (from a file, or from
/// <c>64dec</c>) become U+FFFD; see ADR-0064.
/// </remarks>
internal static class VariableExpansion
{
    /// <summary>curl's <c>MAX_VAR_LEN</c>: a variable name must be shorter than this many bytes.</summary>
    internal const int MaximumNameLength = 128;

    private const string Opening = "{{";

    private const string Closing = "}}";

    /// <summary>
    /// Expands <paramref name="template"/>, adding curl's warnings to <paramref name="options"/>, or refuses
    /// it when a reference names an unknown function or expands to content holding a NUL byte.
    /// </summary>
    /// <param name="options">The options being filled in, holding the variables.</param>
    /// <param name="template">The option's value as given.</param>
    /// <param name="spelledOption">The <c>--expand-</c> option as typed, for the refusal.</param>
    /// <param name="expanded">The expanded value; <paramref name="template"/> itself when nothing was replaced.</param>
    /// <returns><see langword="null"/> when the value was expanded; otherwise the refusal.</returns>
    internal static CommandLineRefusal? Expand(CommandLineOptions options, string template, string spelledOption, out string expanded)
    {
        expanded = template;
        List<byte> output = [];
        bool replaced = false;
        int position = 0;
        bool finished = false;
        while (!finished)
        {
            CommandLineRefusal? refusal = ExpandNext(options, template, spelledOption, output, ref position, ref replaced, out finished);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        if (replaced)
        {
            Append(output, template[position..]);
            expanded = Encoding.UTF8.GetString([.. output]);
        }

        return null;
    }

    /// <summary>
    /// Handles the next <c>{{</c> at or after <paramref name="position"/>: an escaped one, a reference, or one
    /// with no <c>}}</c> after it, which ends the expansion with a warning, as does no <c>{{</c> at all.
    /// </summary>
    private static CommandLineRefusal? ExpandNext(CommandLineOptions options, string template, string spelledOption, List<byte> output, ref int position, ref bool replaced, out bool finished)
    {
        int opening = template.IndexOf(Opening, position, StringComparison.Ordinal);
        int closing = opening < 0 ? -1 : template.IndexOf(Closing, opening, StringComparison.Ordinal);
        finished = closing < 0;
        if (opening > position && template[opening - 1] == '\\')
        {
            Append(output, template[position..(opening - 1)] + Opening);
            position = opening + Opening.Length;
            finished = false;
            return null;
        }

        if (finished)
        {
            WarnOfMissingClose(options, template, opening);
            return null;
        }

        CommandLineRefusal? refusal = ExpandReference(options, template, spelledOption, (position, opening, closing), output, ref replaced);
        position = closing + Closing.Length;
        return refusal;
    }

    private static void WarnOfMissingClose(CommandLineOptions options, string template, int opening)
    {
        if (opening >= 0)
        {
            options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", $"missing close '}}}}' in '{template}'"));
        }
    }

    /// <summary>
    /// Appends the text from the reference's position up to its opening <c>{{</c>,
    /// and then the reference's expansion, or the reference as written when its name is not usable.
    /// </summary>
    private static CommandLineRefusal? ExpandReference(CommandLineOptions options, string template, string spelledOption, (int Position, int Opening, int Closing) reference, List<byte> output, ref bool replaced)
    {
        (int position, int opening, int closing) = reference;
        int nameStart = opening + Opening.Length;
        int colon = template.IndexOf(':', nameStart, closing - nameStart);
        string name = template[nameStart..(colon < 0 ? closing : colon)];
        int nameLength = Encoding.UTF8.GetByteCount(name);
        Append(output, template[position..opening]);
        if (nameLength == 0 || nameLength >= MaximumNameLength)
        {
            options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", $"bad variable name length '{template}'"));
            Append(output, template[opening..(closing + Closing.Length)]);
            return null;
        }

        if (!VariableDefinition.IsName(name))
        {
            options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", $"bad variable name: {name}"));
            Append(output, template[opening..(closing + Closing.Length)]);
            return null;
        }

        CommandLineRefusal? refusal = ExpandVariable(options, template, colon, closing, name, spelledOption, output);
        replaced |= refusal is null;
        return refusal;
    }

    private static CommandLineRefusal? ExpandVariable(CommandLineOptions options, string template, int colon, int closing, string name, string spelledOption, List<byte> output)
    {
        byte[] content = options.FindVariable(name) ?? [];
        if (colon >= 0 && !VariableFunctions.TryApply(template[colon..], content, out content))
        {
            return CommandLineRefusal.VariableExpansionFailure(spelledOption, $"unknown variable function in '{template[colon..closing]}'", options.ErrorsHidden);
        }

        if (Array.IndexOf(content, (byte)0) >= 0)
        {
            return CommandLineRefusal.VariableExpansionFailure(spelledOption, "variable contains null byte", options.ErrorsHidden);
        }

        output.AddRange(content);
        return null;
    }

    private static void Append(List<byte> output, string text) => output.AddRange(Encoding.UTF8.GetBytes(text));
}
