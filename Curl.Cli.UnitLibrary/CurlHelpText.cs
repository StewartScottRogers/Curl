using System.Text;

namespace Curl.Cli;

/// <summary>
/// The lines <c>-h</c> / <c>--help [subject]</c> prints on standard output, ported from curl 8.21.0's
/// <c>tool_help</c> over <see cref="CurlHelpTable"/>: the usage page with no subject, every option for
/// <c>all</c>, the category list for <c>category</c>, one category's options for its name, and the
/// category list after <c>Unknown category provided, ...</c> for anything else. Subjects match in any
/// case. A subject starting with <c>-</c> asks for one option's manual section instead, which
/// <see cref="CurlOptionManualSection"/> prints. The option columns depend on the terminal width curl's
/// <c>get_terminal_columns</c> gives (79 when nothing sets one). The lines carry no line terminator; the
/// console layer chooses the newline (CRLF on Windows, as the mingw reference writes).
/// </summary>
public static class CurlHelpText
{
    /// <summary>The terminal width curl uses when neither <c>COLUMNS</c> nor a console gives one.</summary>
    public const int DefaultColumns = 79;

    /// <summary>The categories <c>--help category</c> lists, in its order, each with its name and one-line description.</summary>
    internal static readonly (string Name, string Description, CurlHelpCategories Category)[] Categories =
    [
        ("auth", "Authentication methods", CurlHelpCategories.Auth),
        ("connection", "Manage connections", CurlHelpCategories.Connection),
        ("curl", "The command line tool itself", CurlHelpCategories.Curl),
        ("deprecated", "Legacy", CurlHelpCategories.Deprecated),
        ("dns", "Names and resolving", CurlHelpCategories.Dns),
        ("file", "FILE protocol", CurlHelpCategories.File),
        ("ftp", "FTP protocol", CurlHelpCategories.Ftp),
        ("global", "Global options", CurlHelpCategories.Global),
        ("http", "HTTP and HTTPS protocol", CurlHelpCategories.Http),
        ("imap", "IMAP protocol", CurlHelpCategories.Imap),
        ("ldap", "LDAP protocol", CurlHelpCategories.Ldap),
        ("output", "File system output", CurlHelpCategories.Output),
        ("pop3", "POP3 protocol", CurlHelpCategories.Pop3),
        ("post", "HTTP POST specific", CurlHelpCategories.Post),
        ("proxy", "Options for proxies", CurlHelpCategories.Proxy),
        ("scp", "SCP protocol", CurlHelpCategories.Scp),
        ("sftp", "SFTP protocol", CurlHelpCategories.Sftp),
        ("smtp", "SMTP protocol", CurlHelpCategories.Smtp),
        ("ssh", "SSH protocol", CurlHelpCategories.Ssh),
        ("telnet", "TELNET protocol", CurlHelpCategories.Telnet),
        ("tftp", "TFTP protocol", CurlHelpCategories.Tftp),
        ("timeout", "Timeouts and delays", CurlHelpCategories.Timeout),
        ("tls", "TLS/SSL related", CurlHelpCategories.Tls),
        ("upload", "Upload, sending data", CurlHelpCategories.Upload),
        ("verbose", "Tracing, logging etc", CurlHelpCategories.Verbose),
    ];

    /// <summary>
    /// Whether <paramref name="subject"/> asks for one option's manual section (<c>--help -v</c>,
    /// <c>--help --verbose</c>) rather than a page this class prints: it starts with <c>-</c>.
    /// </summary>
    /// <param name="subject">The subject given to <c>--help</c>, <see langword="null"/> when none was.</param>
    /// <returns><see langword="true"/> when the subject starts with <c>-</c>.</returns>
    public static bool IsOptionSubject(string? subject) => subject is not null && subject.StartsWith('-');

    /// <summary>Returns the lines <c>--help</c> prints for <paramref name="subject"/> at <paramref name="columns"/> wide.</summary>
    /// <param name="subject">
    /// The subject, <see langword="null"/> or empty for the usage page; <c>all</c>, <c>category</c> or a
    /// category name in any case; anything else gives the unknown-category page.
    /// </param>
    /// <param name="columns">The terminal width, as curl's <c>get_terminal_columns</c> gives it; at least 1.</param>
    /// <returns>The lines, without line terminators.</returns>
    /// <exception cref="ArgumentException"><paramref name="subject"/> starts with <c>-</c> (see <see cref="IsOptionSubject"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columns"/> is less than 1.</exception>
    public static IReadOnlyList<string> Lines(string? subject, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        if (IsOptionSubject(subject))
        {
            throw new ArgumentException("An option subject asks for a manual section, which CurlOptionManualSection prints.", nameof(subject));
        }

        if (string.IsNullOrEmpty(subject))
        {
            return UsageLines(columns);
        }

        return SubjectText(subject, columns).Split('\n')[..^1];
    }

    private static string SubjectText(string subject, int columns)
    {
        if (subject.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return CategoryOptionsText(CurlHelpCategories.All, columns);
        }

        if (subject.Equals("category", StringComparison.OrdinalIgnoreCase))
        {
            return CategoryListText();
        }

        foreach ((string name, string description, CurlHelpCategories category) in Categories)
        {
            if (subject.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return $"{name}: {description}\n" + CategoryOptionsText(category, columns);
            }
        }

        return "Unknown category provided, here is a list of all categories:\n\n" + CategoryListText();
    }

    private static IReadOnlyList<string> UsageLines(int columns)
    {
        string text =
            "Usage: curl [options...] <url>\n"
            + CategoryOptionsText(CurlHelpCategories.Important, columns)
            + "\nThis is not the full help; this menu is split into categories.\n"
            + "Use \"--help category\" to get an overview of all categories, which are:\n"
            + CategoryNamesText(columns)
            + "Use \"--help all\" to list all options\n"
            + "Use \"--help [option]\" to view documentation for a given option\n";
        return text.Split('\n')[..^1];
    }

    /// <summary>curl's <c>print_category</c>: each row in the category, its option padded to a shared width.</summary>
    private static string CategoryOptionsText(CurlHelpCategories category, int columns)
    {
        List<CurlHelpEntry> entries = [.. CurlHelpTable.Entries.Where(entry => (entry.Categories & category) != 0)];
        int longestOption = Math.Max(5, entries.Max(entry => entry.Option.Length));
        int longestDescription = Math.Max(5, entries.Max(entry => entry.Description.Length));
        int optionWidth = SharedOptionWidth(longestOption, longestDescription, columns);

        StringBuilder text = new();
        foreach (CurlHelpEntry entry in entries)
        {
            int width = RowOptionWidth(optionWidth, entry.Description.Length, columns);
            text.Append(' ').Append(entry.Option.PadRight(width)).Append("  ").Append(entry.Description).Append('\n');
        }

        return text.ToString();
    }

    private static int SharedOptionWidth(int longestOption, int longestDescription, int columns)
    {
        if (longestDescription > columns)
        {
            return 0;
        }

        return longestOption + longestDescription > columns ? columns - longestDescription : longestOption;
    }

    /// <summary>Narrows one row's option width so the row does not wrap, as curl does per row.</summary>
    private static int RowOptionWidth(int optionWidth, int descriptionLength, int columns)
    {
        if (columns < 2 || optionWidth + descriptionLength < columns - 2)
        {
            return optionWidth;
        }

        return descriptionLength < columns - 2 ? columns - 3 - descriptionLength : 0;
    }

    /// <summary>curl's <c>get_categories</c>: one line per category, the name padded to 11.</summary>
    private static string CategoryListText()
    {
        StringBuilder text = new();
        foreach ((string name, string description, _) in Categories)
        {
            text.Append(' ').Append(name.PadRight(11)).Append(' ').Append(description).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// curl's <c>get_categories_list</c>: the names joined with <c>, </c> and wrapped before
    /// <paramref name="columns"/>, the last followed by a full stop; a wrapped line keeps its trailing space.
    /// </summary>
    private static string CategoryNamesText(int columns)
    {
        StringBuilder text = new();
        int column = 0;
        for (int index = 0; index < Categories.Length - 1; index++)
        {
            string name = Categories[index].Name;
            if (column + name.Length + 2 < columns)
            {
                column += name.Length + 2;
            }
            else
            {
                text.Append('\n');
                column = name.Length + 2;
            }

            text.Append(name).Append(", ");
        }

        string last = Categories[^1].Name;
        return text.Append(column + last.Length + 1 < columns ? string.Empty : "\n").Append(last).Append(".\n").ToString();
    }
}
