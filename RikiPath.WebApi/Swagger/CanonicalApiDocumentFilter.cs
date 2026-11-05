using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RikiPath.WebApi.Swagger;

/// <summary>
/// Loại các nhóm Swagger rỗng sau khi API được gom theo nghiệp vụ.
/// </summary>
public sealed class CanonicalApiDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // XML comment tự sinh có thể để lại tag của controller cũ dù mọi
        // operation đã được gom vào nhóm nghiệp vụ. Xóa tag mồ côi để Swagger
        // không hiện những nhóm rỗng gây cảm giác có API trùng.
        var usedTags = swaggerDoc.Paths.Values
            .SelectMany(path => path.Operations.Values)
            .SelectMany(operation => operation.Tags)
            .Select(tag => tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (swaggerDoc.Tags is not null)
        {
            foreach (var unusedTag in swaggerDoc.Tags.Where(tag => !usedTags.Contains(tag.Name)).ToArray())
            {
                swaggerDoc.Tags.Remove(unusedTag);
            }

            foreach (var tag in swaggerDoc.Tags)
            {
                tag.Description = $"Nhóm chức năng {tag.Name} của API RikiPath.";
            }
        }
    }

}
