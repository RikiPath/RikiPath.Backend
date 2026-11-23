namespace RikiPath.Application.IServices;

public sealed record TemplateFileResult(byte[] Bytes, string ContentType, string FileName);

public interface ISkillTemplateService
{
    TemplateFileResult GenerateTemplate(string templateKind, string format);
}
