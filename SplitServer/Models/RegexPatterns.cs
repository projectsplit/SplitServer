using System.Text.RegularExpressions;

namespace SplitServer.Models;

public partial class RegexPatterns
{
    [GeneratedRegex(@"^([+-]\d+)([+-]\d+)$")]
    public static partial Regex Dms();
}