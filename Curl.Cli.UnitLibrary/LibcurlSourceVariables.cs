namespace Curl.Cli;

/// <summary>
/// The <c>curl_slist</c> and <c>curl_mime</c> variables a <see cref="LibcurlSourceCode" /> file uses, as curl
/// 8.21.0 writes them: numbered from 1 per kind across every transfer in the order they are made, each
/// declared at the top of <c>main</c>, set to <c>NULL</c> (and a list filled) before <c>curl_easy_init</c>,
/// and freed after <c>curl_easy_cleanup</c>, all three in the order the variables were made (measured
/// 2026-10-01, BL-653 Notes).
/// </summary>
internal sealed class LibcurlSourceVariables
{
    private int stringLists;
    private int mimes;

    /// <summary>The declaration lines, in the order the variables were made.</summary>
    public List<string> Declarations { get; } = [];

    /// <summary>The lines that set each variable to <c>NULL</c> and fill each list, in the order the variables were made.</summary>
    public List<string> Initialisations { get; } = [];

    /// <summary>The lines that free each variable, in the order the variables were made.</summary>
    public List<string> Cleanups { get; } = [];

    /// <summary>Makes the next <c>slist</c> variable, filled with <paramref name="items" /> in order.</summary>
    /// <param name="items">The strings appended to the list.</param>
    /// <returns>The variable's name, <c>slist</c> and its number.</returns>
    public string AddStringList(IEnumerable<string> items)
    {
        string name = $"slist{++stringLists}";
        Declarations.Add($"  struct curl_slist *{name};");
        Initialisations.Add($"  {name} = NULL;");
        Initialisations.AddRange(items.Select(item => $"  {name} = curl_slist_append({name}, {LibcurlSourceCode.QuoteCString(item)});"));
        Cleanups.Add($"  curl_slist_free_all({name});");
        Cleanups.Add($"  {name} = NULL;");
        return name;
    }

    /// <summary>Makes the next <c>mime</c> variable and the <c>part</c> variable of the same number that goes with it.</summary>
    /// <returns>The number both variables carry.</returns>
    public int AddMime()
    {
        int number = ++mimes;
        Declarations.Add($"  curl_mime *mime{number};");
        Declarations.Add($"  curl_mimepart *part{number};");
        Initialisations.Add($"  mime{number} = NULL;");
        Cleanups.Add($"  curl_mime_free(mime{number});");
        Cleanups.Add($"  mime{number} = NULL;");
        return number;
    }
}
