using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using uMedia.Interfaces;

namespace uMedia.Services;

/// <summary>
/// A JSON file on disk exposed as an <see cref="IDataProvider{T}"/>.
/// Everything lives next to the executable, so the app stays portable.
/// </summary>
public class JsonParser<T> : IDataProvider<T> where T : new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string path;
    private T data;

    public event DataChangedEvent<T>? DataChanged;

    public JsonParser(string fileName)
    {
        var folder = AppContext.BaseDirectory;
        path = Path.Combine(folder, fileName);
        data = Read();
    }

    public T Get() => data;

    public void Save(T value)
    {
        data = value;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception e)
        {
            Log.Error($"Could not write {path}", e);
        }

        DataChanged?.Invoke(value);
    }

    private T Read()
    {
        try
        {
            if (!File.Exists(path)) return new T();

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (Exception e)
        {
            Log.Error($"Could not read {path}, falling back to defaults", e);

            return new T();
        }
    }
}

public class AppSettingsProvider(string fileName = "settings.json")
    : JsonParser<Models.AppSettings>(fileName), IAppSettingsProvider { }

public class WidgetLayoutProvider(string fileName = "layout.json")
    : JsonParser<Models.WidgetLayout>(fileName), IWidgetLayoutProvider { }
