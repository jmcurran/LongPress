using System.Text;
using System.Text.Json;

namespace LongPress;

internal sealed class Settings
{
    public int HoldDelayMs { get; set; } = 400;
    public string Theme { get; set; } = "auto";
    public List<string> ExcludedProcesses { get; set; } = new();
    public Dictionary<string, string> Accents { get; set; } = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LongPress", "settings.json");

    // The default settings file, written on first run. It is also the built-in fallback.
    private const string DefaultJson = """
        {
          // How long (milliseconds) a key must be held before the accent popup appears.
          // The popup can't appear before Windows starts repeating the key, so the
          // "Repeat delay" slider in Control Panel > Keyboard also matters.
          "holdDelayMs": 400,

          // Popup colours: "auto" follows Windows' light/dark app mode, or "light" / "dark".
          "theme": "auto",

          // Programs where long-press is switched off (process name, with or without .exe).
          "excludedProcesses": [ "mstsc", "vmconnect" ],

          // Character -> variants, in popup order (at most 9). Spaces are ignored.
          // Capitals are derived automatically; add an upper-case key (e.g. "A")
          // only if you want a different list for capitals.
          // French, Spanish, German, Dutch, Danish, Czech, Māori.
          "accents": {
            "a": "à á â ä å æ ā",
            "c": "ç č",
            "d": "ď",
            "e": "é è ê ë ě ē",
            "i": "í ì î ï ī",
            "n": "ñ ň",
            "o": "ó ò ô ö ø œ ō",
            "r": "ř",
            "s": "ß š",
            "t": "ť",
            "u": "ú ù û ü ů ū",
            "y": "ý ÿ",
            "z": "ž",
            "?": "¿",
            "!": "¡",
            "\"": "« »"
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static Settings Parse(string json) =>
        JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? throw new JsonException("Empty settings file.");

    public static Settings Defaults() => Parse(DefaultJson);

    /// <summary>Loads the settings file, creating it on first run. Returns an error message on failure.</summary>
    public static (Settings settings, string? error) Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, DefaultJson, new UTF8Encoding(false));
            }
            return (Parse(File.ReadAllText(FilePath, Encoding.UTF8)), null);
        }
        catch (Exception ex)
        {
            return (Defaults(), $"Couldn't read {FilePath}; using the built-in defaults.\n{ex.Message}");
        }
    }

    public bool IsExcluded(string processName) =>
        ExcludedProcesses.Any(p => string.Equals(
            p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p,
            processName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The variants for a typed character, or null if it has none.</summary>
    public string? VariantsFor(char c)
    {
        if (Accents.TryGetValue(c.ToString(), out var explicitList))
        {
            string cleaned = Clean(explicitList);
            return cleaned.Length > 0 ? cleaned : null;
        }

        if (char.IsUpper(c) && Accents.TryGetValue(char.ToLowerInvariant(c).ToString(), out var lowerList))
        {
            var sb = new StringBuilder();
            foreach (char v in Clean(lowerList))
            {
                char u = char.ToUpperInvariant(v);
                if (char.IsLetter(u) && !char.IsUpper(u)) continue; // e.g. ß has no single-letter capital
                if (sb.ToString().IndexOf(u) < 0) sb.Append(u);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        return null;
    }

    private static string Clean(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s)
            if (!char.IsWhiteSpace(ch) && sb.ToString().IndexOf(ch) < 0 && sb.Length < 9)
                sb.Append(ch);
        return sb.ToString();
    }
}
