using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Newtonsoft.Json;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Themes;

public sealed class ThemeManager : IDisposable
{
    public static ThemeManager? Active { get; private set; }
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly string directory;
    private readonly Dictionary<string, ThemeDefinition> themes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IDalamudTextureWrap> images = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadingImages = new(StringComparer.Ordinal);
    private readonly HashSet<string> failedImages = new(StringComparer.Ordinal);
    private int imageGeneration;
    public string? Error { get; private set; }
    public IReadOnlyCollection<string> Names => themes.Keys;
    public string DirectoryPath => directory;

    public ThemeManager(string configDirectory)
    {
        Active = this;
        directory = Path.Combine(configDirectory, "themes");
        Directory.CreateDirectory(directory);
        Reload();
    }

    public void Reload()
    {
        Error = null;
        imageGeneration++;
        foreach (var image in images.Values) image.Dispose();
        images.Clear();
        loadingImages.Clear();
        failedImages.Clear();
        themes.Clear();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BlackJackButtler.Themes.Base.json")!;
        using var reader = new StreamReader(stream);
        themes["Base"] = JsonConvert.DeserializeObject<ThemeDefinition>(reader.ReadToEnd())!;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var theme = Parse(File.ReadAllText(path));
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, "Base", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Base is reserved");
                theme.Name = name;
                themes[name] = theme;
            }
            catch (Exception ex)
            {
                Error = $"{Path.GetFileName(path)}: {ex.Message}";
            }
        }
    }

    public ThemeDefinition Get(string name) => themes.TryGetValue(name, out var theme) ? theme : themes["Base"];

    public ThemeDefinition Resolve(IEnumerable<ThemeLayer> layers, ThemeArea area)
    {
        var result = Clone(Get("Base"));
        result.Colors.Clear();
        foreach (var layer in layers.Reverse())
        {
            if (!layer.Enabled || (layer.Areas & area) == 0 || !themes.TryGetValue(layer.Name, out var theme)) continue;
            foreach (var pair in theme.Fonts) result.Fonts[pair.Key] = pair.Value;
            foreach (var pair in theme.Colors) result.Colors[pair.Key] = pair.Value;
            foreach (var pair in theme.Corners) result.Corners[pair.Key] = pair.Value;
            result.BackgroundImage = theme.BackgroundImage;
        }
        return result;
    }

    public static ThemeDefinition Parse(string json)
    {
        var theme = JsonConvert.DeserializeObject<ThemeDefinition>(json) ?? throw new InvalidDataException("Empty theme");
        theme.Fonts ??= new(StringComparer.OrdinalIgnoreCase);
        theme.Colors ??= new(StringComparer.OrdinalIgnoreCase);
        theme.Corners ??= new(StringComparer.OrdinalIgnoreCase);
        if (theme.Fonts.Values.Any(value => value == null || !float.IsFinite(value.Size) || value.Size <= 0 || (value.Family != "Default" && value.Family != "Mono") || (!string.IsNullOrEmpty(value.Color) && !ThemeDefinition.TryColor(value.Color, out _)))) throw new InvalidDataException("Invalid font settings");
        if (theme.Colors.Any(pair => !Enum.TryParse<ImGuiCol>(pair.Key, true, out _) || !ThemeDefinition.TryColor(pair.Value, out _))) throw new InvalidDataException("Invalid color");
        if (theme.Corners.Values.Any(value => value == null || value.Rounding is float rounding && (!float.IsFinite(rounding) || rounding < 0))) throw new InvalidDataException("Invalid corner settings");
        if (!string.IsNullOrEmpty(theme.HeaderColor) && !ThemeDefinition.TryColor(theme.HeaderColor, out _)) throw new InvalidDataException("Invalid header color");
        theme.BackgroundImage ??= "";
        return theme;
    }

    private static ThemeDefinition Clone(ThemeDefinition theme) => JsonConvert.DeserializeObject<ThemeDefinition>(JsonConvert.SerializeObject(theme))!;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Equals("Base", StringComparison.OrdinalIgnoreCase)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
            throw new InvalidDataException("Invalid theme name");
    }

    public void Save(string name, ThemeDefinition theme)
    {
        ValidateName(name);
        theme.Name = name;
        Parse(JsonConvert.SerializeObject(theme));
        var path = Path.Combine(directory, name + ".json");
        File.WriteAllText(path, JsonConvert.SerializeObject(theme, Formatting.Indented));
        themes[name] = theme;
    }

    public void SaveJson(string name, string json)
    {
        var theme = Parse(json);
        Save(name, theme);
    }

    public string Export(string name) => JsonConvert.SerializeObject(Get(name), Formatting.Indented);

    public string Import(string json, string fallbackName)
    {
        var theme = Parse(json);
        var preferred = string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Equals("Base", StringComparison.OrdinalIgnoreCase) ? fallbackName : theme.Name;
        ValidateName(preferred);
        var name = preferred;
        for (var suffix = 2; themes.ContainsKey(name) || File.Exists(Path.Combine(directory, name + ".json")); suffix++)
            name = $"{preferred} {suffix}";
        Save(name, theme);
        return name;
    }

    public void Rename(string oldName, string newName)
    {
        if (oldName.Equals("Base", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Base is locked");
        ValidateName(newName);
        if (themes.ContainsKey(newName)) throw new IOException("Theme name already exists");
        File.Move(Path.Combine(directory, oldName + ".json"), Path.Combine(directory, newName + ".json"));
        var theme = themes[oldName];
        themes.Remove(oldName);
        Save(newName, theme);
    }

    public void Delete(string name)
    {
        if (name.Equals("Base", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Base is locked");
        File.Delete(Path.Combine(directory, name + ".json"));
        themes.Remove(name);
    }

    public string CreateCopy(string name)
    {
        ValidateName(name);
        var path = Path.Combine(directory, name + ".json");
        if (themes.ContainsKey(name) || File.Exists(path)) throw new IOException("Theme file already exists");
        var theme = Clone(Get("Base"));
        Save(name, theme);
        return name;
    }

    public IDalamudTextureWrap? GetImage(ThemeDefinition theme)
    {
        var source = theme.BackgroundImage?.Trim() ?? "";
        if (string.IsNullOrEmpty(source)) return null;
        if (images.TryGetValue(source, out var image)) return image;
        if (failedImages.Contains(source)) return null;
        if (loadingImages.Add(source)) _ = LoadImage(source, imageGeneration);
        return null;
    }

    private async Task LoadImage(string source, int generation)
    {
        try
        {
            byte[] bytes;
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 10_000_000) throw new InvalidDataException("Image exceeds 10 MB");
                bytes = await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                var path = Path.IsPathRooted(source) ? source : Path.Combine(directory, source);
                bytes = await File.ReadAllBytesAsync(path);
            }
            if (bytes.Length > 10_000_000) throw new InvalidDataException("Image exceeds 10 MB");
            var loaded = await Plugin.TextureProvider.CreateFromImageAsync(bytes, "Theme background");
            if (generation == imageGeneration) images[source] = loaded;
            else loaded.Dispose();
        }
        catch (Exception ex)
        {
            if (generation == imageGeneration)
            {
                Error = ex.Message;
                failedImages.Add(source);
            }
        }
        finally
        {
            if (generation == imageGeneration) loadingImages.Remove(source);
        }
    }

    public void Dispose()
    {
        if (ReferenceEquals(Active, this)) Active = null;
        imageGeneration++;
        foreach (var image in images.Values) image.Dispose();
        images.Clear();
        loadingImages.Clear();
        failedImages.Clear();
    }
}
