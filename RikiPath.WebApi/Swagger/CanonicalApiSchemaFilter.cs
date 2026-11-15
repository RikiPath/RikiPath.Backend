using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RikiPath.WebApi.Swagger;

/// <summary>
/// Chuẩn hóa phần mô tả schema bằng tiếng Việt.
/// </summary>
public sealed class CanonicalApiSchemaFilter : ISchemaFilter
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var typeName = Humanize(context.Type.Name.Split('`')[0]);
        if (string.IsNullOrWhiteSpace(schema.Description) || schema.Description.StartsWith("Cấu trúc dữ liệu"))
        {
            schema.Description = $"Mô hình dữ liệu {typeName} trong hệ thống RikiPath.";
        }

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            if (string.IsNullOrWhiteSpace(propertySchema.Description) || propertySchema.Description.StartsWith("Giá trị của trường"))
            {
                propertySchema.Description = $"Thông tin {Humanize(propertyName)}.";
            }
        }
    }

    private static string Humanize(string value)
    {
        var words = WordBoundary.Replace(value.Replace('-', ' ').Replace('_', ' '), "$1 $2");
        return words.Length == 0 ? "API" : char.ToLowerInvariant(words[0]) + words[1..];
    }
}
