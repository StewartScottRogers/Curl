using Curl.Conformance.UnitProgram.ToolParsers;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the ToolParsers area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] ToolParsersPrograms =>
    [
        new Tool1604SanitizeFileName(),
        new Tool1621StripCredentials(),
        new Tool1622ProgressFields(),
        new Tool1623SizeParameter(),
        new Unit1636ProgressMeterFields(),
        new Unit1668Ftp213Date(),
        new Tool1720DirectoryHierarchy(),
        new Unit2604GetPathname(),
        new Unit2605SshRange(),
        new Unit3200GetLine(),
        new Unit3227MailPartLineBreaks(),
    ];
}
