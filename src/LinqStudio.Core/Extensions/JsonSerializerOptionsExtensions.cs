using System.Text.Json;

namespace LinqStudio.Core.Extensions;

public static class JsonSerializerOptionsExtensions
{
    private static readonly JsonSerializerOptions _indentedOptions = new() { WriteIndented = true };

    extension(JsonSerializerOptions options)
    {
        public static JsonSerializerOptions Indented => _indentedOptions;
    }
}
