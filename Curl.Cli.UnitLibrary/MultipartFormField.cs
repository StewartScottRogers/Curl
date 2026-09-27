namespace Curl.Cli;

/// <summary>
/// Reads one <c>-F</c> / <c>--form</c> or <c>--form-string</c> value into
/// <see cref="CommandLineOptions.FormParts"/>, as curl 8.21.0's <c>formparse</c>
/// (<c>src/tool_formparse.c</c>) does, and selects the multipart form post. The value is
/// <c>name=content</c>: the name runs to the first <c>=</c> and may be empty. For <c>-F</c> the
/// content is one of
/// <list type="bullet">
/// <item><c>(</c>, which opens a <see cref="FormPartKind.Multipart"/> part that takes the parts after
/// it, and <c>=)</c> (no name), which closes the innermost one;</item>
/// <item><c>@file</c>, a <see cref="FormPartKind.FileUpload"/> part, or <c>@file1,file2</c>, a
/// <see cref="FormPartKind.Multipart"/> part holding one per file;</item>
/// <item><c>&lt;file</c>, a <see cref="FormPartKind.FileContent"/> part;</item>
/// <item>anything else, a <see cref="FormPartKind.Text"/> part;</item>
/// </list>
/// each followed by the <c>;type=</c>, <c>;filename=</c>, <c>;encoder=</c> and <c>;headers=</c>
/// parameters <see cref="FormPartParameterReader"/> reads. <c>--form-string</c> takes the whole
/// content as text. Files are named, not read, except a <c>;headers=@file</c> file.
/// </summary>
/// <remarks>
/// Refusals, measured with the local curl 8.21.0 on 2026-09-26: a value with no <c>=</c>
/// (<c>-F abc</c>, <c>-F ''</c>, <c>--form=</c>, <c>--form-string abc</c>) prints
/// <c>Warning: Illegally formatted input field</c>, and <c>=)</c> with no multipart open prints
/// <c>Warning: no multipart to terminate</c>; each is then refused with
/// <c>curl: option &lt;as typed&gt;: is badly used here</c> and exit 2. <c>-s</c> read before drops the
/// warning line. Both options select the multipart form post, so a different method selected
/// earlier refuses them (see <see cref="CommandLineOptionTable.SelectRequestMethod"/>).
/// </remarks>
internal static class MultipartFormField
{
    /// <summary>
    /// Adds the parts <paramref name="value"/> describes to <paramref name="options"/> and selects the
    /// multipart form post, or refuses the value.
    /// </summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="literal"><see langword="true"/> for <c>--form-string</c>, whose content is never parsed.</param>
    /// <param name="spelledOption">The option as typed, for naming it in a refusal.</param>
    /// <param name="dataFileReader">Reads a <c>;headers=@file</c> file.</param>
    /// <returns><see langword="null"/> when the value was applied; otherwise why it was refused.</returns>
    internal static CommandLineRefusal? Apply(CommandLineOptions options, string value, bool literal, string spelledOption, IDataFileReader dataFileReader)
    {
        int equals = value.IndexOf('=', StringComparison.Ordinal);
        if (equals < 0)
        {
            return Refuse(options, "Illegally formatted input field", spelledOption);
        }

        string? name = equals > 0 ? value[..equals] : null;
        string content = value[(equals + 1)..];
        if (!AddParts(options, name, content, literal, dataFileReader))
        {
            return Refuse(options, "no multipart to terminate", spelledOption);
        }

        return CommandLineOptionTable.SelectRequestMethod(options, SelectedHttpMethod.MultipartFormPost, spelledOption);
    }

    /// <summary>Adds the parts <paramref name="content"/> describes; <see langword="false"/> for <c>=)</c> with no multipart open.</summary>
    private static bool AddParts(CommandLineOptions options, string? name, string content, bool literal, IDataFileReader dataFileReader)
    {
        FormPartParameterReader reader = new(options, content, dataFileReader);
        if (literal)
        {
            AddNamed(options, name, new FormPartSpecification(FormPartKind.Text, content, null, null, null, []));
        }
        else if (content.StartsWith('('))
        {
            OpenMultipart(options, name, reader);
        }
        else if (name is null && content == ")")
        {
            return options.TryCloseMultipart();
        }
        else if (content.StartsWith('@'))
        {
            AddFileUploads(options, name, reader);
        }
        else
        {
            AddTextOrFileContent(options, name, content.StartsWith('<'), reader);
        }

        return true;
    }

    private static void OpenMultipart(CommandLineOptions options, string? name, FormPartParameterReader reader)
    {
        FormPartParameters parameters = reader.Read(endCharacter: '\0', fileNameAllowed: false, encoderAllowed: false);
        FormPartSpecification multipart = new(FormPartKind.Multipart, string.Empty, parameters.ContentType, null, null, parameters.Headers)
        {
            Name = name,
        };
        options.OpenMultipart(multipart);
    }

    /// <summary>
    /// Adds <c>@file</c> as one <see cref="FormPartKind.FileUpload"/> part, or <c>@file1,file2</c> as a
    /// <see cref="FormPartKind.Multipart"/> part holding one per file; the name goes on the outer part.
    /// Each file has its own parameters, and a separator other than a comma ends the list.
    /// </summary>
    private static void AddFileUploads(CommandLineOptions options, string? name, FormPartParameterReader reader)
    {
        FormPartSpecification? group = null;
        FormPartParameters parameters;
        do
        {
            reader.SkipSeparator();
            parameters = reader.Read(endCharacter: ',', fileNameAllowed: true, encoderAllowed: true);
            FormPartSpecification file = FromParameters(FormPartKind.FileUpload, parameters);
            if (group is null && parameters.Separator == ',')
            {
                group = new FormPartSpecification(FormPartKind.Multipart, string.Empty, null, null, null, []) { Name = name };
                options.AddFormPart(group);
            }

            AddToGroupOrNamed(options, group, name, file);
        }
        while (parameters.Separator == ',');
    }

    private static void AddToGroupOrNamed(CommandLineOptions options, FormPartSpecification? group, string? name, FormPartSpecification file)
    {
        if (group is null)
        {
            AddNamed(options, name, file);
        }
        else
        {
            group.AddPart(file);
        }
    }

    /// <summary>
    /// Adds a <see cref="FormPartKind.FileContent"/> part for <c>&lt;file</c>, where <c>;filename=</c>
    /// is not allowed, or else a <see cref="FormPartKind.Text"/> part, then warns about anything left
    /// after the parameters.
    /// </summary>
    private static void AddTextOrFileContent(CommandLineOptions options, string? name, bool fileContent, FormPartParameterReader reader)
    {
        if (fileContent)
        {
            reader.SkipSeparator();
        }

        FormPartParameters parameters = reader.Read(endCharacter: '\0', fileNameAllowed: !fileContent, encoderAllowed: true);
        AddNamed(options, name, FromParameters(fileContent ? FormPartKind.FileContent : FormPartKind.Text, parameters));
        if (parameters.Separator != '\0')
        {
            reader.Warn($"garbage at end of field specification: {reader.Rest}");
        }
    }

    private static FormPartSpecification FromParameters(FormPartKind kind, FormPartParameters parameters) =>
        new(kind, parameters.Data, parameters.ContentType, parameters.FileName, parameters.Encoder, parameters.Headers);

    private static void AddNamed(CommandLineOptions options, string? name, FormPartSpecification part)
    {
        part.Name = name;
        options.AddFormPart(part);
    }

    private static CommandLineRefusal Refuse(CommandLineOptions options, string warning, string spelledOption)
    {
        options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", warning));
        return CommandLineRefusal.BadlyUsedHere(spelledOption);
    }
}
