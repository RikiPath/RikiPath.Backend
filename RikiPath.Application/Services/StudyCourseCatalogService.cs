using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    public Task<ApiResponse<object>> MentorsAsync(CancellationToken ct) => Run(async () =>
    {
        Admin();
        var mentors = await uow.UserAccounts.Query().Where(x => x.Role == Role.Mentor && !x.IsDeleted)
            .Select(x => new { x.Id, x.FirstName, x.LastName }).ToListAsync(ct);
        return mentors.Select(m => new { m.Id, Name = $"{m.FirstName} {m.LastName}".Trim() }).ToList();
    });
    public Task<ApiResponse<object>> AdminCatalogAsync(CancellationToken ct) => Run(async () =>
    {
        Admin();
        return new {
            Types = await uow.CertificateTypes.Query().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.Description, x.SortOrder, IsActive = x.IsActive && !x.IsDeleted }).ToListAsync(ct),
            Levels = await uow.CertificateLevels.Query().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.CertificateTypeId, x.Code, x.Description, x.SortOrder, IsActive = !x.IsDeleted }).ToListAsync(ct)
        };
    });
    public Task<ApiResponse<object>> SaveCertificateAsync(int? id, SaveCourseCertificate r, CancellationToken ct) => Run(async () =>
    {
        Admin(); Require(!string.IsNullOrWhiteSpace(r.Name) && r.Name.Length <= 100 && (r.Description?.Length ?? 0) <= 2000, "Kiểm tra tên và mô tả chứng chỉ.");
        var name = r.Name.Trim();
        Require(!await uow.CertificateTypes.Query().AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct), "Tên chứng chỉ đã tồn tại.");
        var type = id.HasValue ? await uow.CertificateTypes.GetByIdAsync(id.Value, ct) : new CertificateType();
        Require(type is not null, "Không tìm thấy chứng chỉ.");
        type!.Name = name; type.Description = r.Description; type.SortOrder = r.SortOrder; type.IsActive = r.IsActive; type.IsDeleted = false;
        if (!id.HasValue) await uow.CertificateTypes.AddAsync(type, ct);
        await uow.SaveChangesAsync(ct); return new { type.Id };
    });
    public Task<ApiResponse<object>> SaveLevelAsync(int? id, SaveCourseLevel r, CancellationToken ct) => Run(async () =>
    {
        Admin(); Require(!string.IsNullOrWhiteSpace(r.Code) && r.Code.Length <= 50 && (r.Description?.Length ?? 0) <= 2000, "Kiểm tra mã và mô tả cấp độ.");
        Require(await uow.CertificateTypes.Query().AnyAsync(x => x.Id == r.CertificateTypeId && !x.IsDeleted, ct), "Chứng chỉ không hợp lệ.");
        var code = r.Code.Trim().ToUpperInvariant();
        Require(!await uow.CertificateLevels.Query().AnyAsync(x => x.Id != id && x.CertificateTypeId == r.CertificateTypeId && x.Code.ToUpper() == code, ct), "Mã cấp độ đã tồn tại trong chứng chỉ này.");
        var level = id.HasValue ? await uow.CertificateLevels.GetByIdAsync(id.Value, ct) : new CertificateLevel();
        Require(level is not null, "Không tìm thấy cấp độ.");
        // A renamed level would change the meaning of existing lessons, qualifications and paid courses.
        Require(!id.HasValue || (level!.Code == code && level.CertificateTypeId == r.CertificateTypeId), "Giữ nguyên mã và chứng chỉ của cấp độ đã tạo. Hãy tạo cấp độ mới nếu nghiệp vụ khác.");
        level!.Code = code; level.CertificateTypeId = r.CertificateTypeId; level.Description = r.Description; level.SortOrder = r.SortOrder; level.IsDeleted = !r.IsActive;
        if (!id.HasValue) await uow.CertificateLevels.AddAsync(level, ct);
        await uow.SaveChangesAsync(ct); return new { level.Id };
    });
}
