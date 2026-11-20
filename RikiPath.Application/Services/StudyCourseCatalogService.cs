using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    private const int CatalogNameMaxLength = 100;
    private const int CatalogCodeMaxLength = 50;
    private const int CatalogDescriptionMaxLength = 2000;

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    private static string CollapseSpaces(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : MultiSpaceRegex().Replace(value.Trim(), " ");

    private static string? NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ComputeOrderToken(IEnumerable<(int Id, int SortOrder, int Revision)> rows)
        => string.Join(';', rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .Select(r => $"{r.Id}:{r.SortOrder}:{r.Revision}"));

    private async Task RecordAdminAuditAsync(
        string entityType,
        int entityId,
        string action,
        object payload,
        CancellationToken ct)
    {
        if (uow.AdminAuditLogs is null) return;
        await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            ActorUserId = Actor.Id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);
        await uow.SaveChangesAsync(ct);
    }

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
        var types = await uow.CertificateTypes.Query()
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.SortOrder,
                IsActive = x.IsActive && !x.IsDeleted,
                x.Revision,
            })
            .ToListAsync(ct);

        var levels = await uow.CertificateLevels.Query()
            .AsNoTracking()
            .OrderBy(x => x.CertificateTypeId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.CertificateTypeId,
                x.Code,
                x.Description,
                x.SortOrder,
                IsActive = !x.IsDeleted,
                x.Revision,
            })
            .ToListAsync(ct);

        var typesOrderToken = ComputeOrderToken(types.Select(x => (x.Id, x.SortOrder, x.Revision)));
        var levelsOrderTokens = levels
            .GroupBy(x => x.CertificateTypeId)
            .ToDictionary(
                g => g.Key,
                g => ComputeOrderToken(g.Select(x => (x.Id, x.SortOrder, x.Revision))));

        return new
        {
            Types = types,
            Levels = levels,
            TypesOrderToken = typesOrderToken,
            LevelsOrderTokens = levelsOrderTokens,
        };
    });

    public Task<ApiResponse<object>> SaveCertificateAsync(int? id, SaveCourseCertificate r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var name = CollapseSpaces(r.Name);
        Rule(!string.IsNullOrWhiteSpace(name) && name.Length <= CatalogNameMaxLength,
            "CERTIFICATE_NAME_INVALID",
            $"Tên chứng chỉ là bắt buộc và tối đa {CatalogNameMaxLength} ký tự.",
            HttpStatusCode.BadRequest);

        var description = NormalizeDescription(r.Description);
        Rule((description?.Length ?? 0) <= CatalogDescriptionMaxLength,
            "CERTIFICATE_DESCRIPTION_TOO_LONG",
            $"Mô tả chứng chỉ tối đa {CatalogDescriptionMaxLength} ký tự.",
            HttpStatusCode.BadRequest);

        if (r.SortOrder.HasValue && r.SortOrder.Value < 1)
        {
            Rule(false,
                "DISPLAY_ORDER_INVALID",
                "Thứ tự hiển thị phải là số nguyên dương bắt đầu từ 1.",
                HttpStatusCode.BadRequest);
        }

        CertificateType saved = null!;
        string orderToken = string.Empty;

        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                await uow.CertificateTypes.LockOrderScopeAsync(token);

                var allTypes = await uow.CertificateTypes.Query()
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToListAsync(token);

                Rule(!allTypes.Any(x => x.Id != id && string.Equals(CollapseSpaces(x.Name), name, StringComparison.OrdinalIgnoreCase)),
                    "CERTIFICATE_ALREADY_EXISTS",
                    "Tên chứng chỉ đã tồn tại.",
                    HttpStatusCode.Conflict);

                CertificateType? existing = null;
                if (id.HasValue)
                {
                    existing = allTypes.FirstOrDefault(x => x.Id == id.Value);
                    Rule(existing is not null,
                        "CERTIFICATE_NOT_FOUND",
                        "Không tìm thấy chứng chỉ.",
                        HttpStatusCode.NotFound);

                    if (r.ExpectedRevision.HasValue)
                    {
                        Rule(existing!.Revision == r.ExpectedRevision.Value,
                            "ORDER_REVISION_CONFLICT",
                            "Dữ liệu chứng chỉ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                            HttpStatusCode.Conflict);
                    }
                }

                if (!string.IsNullOrWhiteSpace(r.ExpectedOrderToken))
                {
                    var currentToken = ComputeOrderToken(allTypes.Select(x => (x.Id, x.SortOrder, x.Revision)));
                    Rule(string.Equals(currentToken, r.ExpectedOrderToken, StringComparison.Ordinal),
                        "ORDER_REVISION_CONFLICT",
                        "Thứ tự chứng chỉ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                var maxOrder = id.HasValue ? Math.Max(1, allTypes.Count) : allTypes.Count + 1;
                int targetOrder;
                if (!id.HasValue && !r.SortOrder.HasValue)
                {
                    targetOrder = maxOrder;
                }
                else
                {
                    Rule(r.SortOrder.HasValue && r.SortOrder.Value >= 1 && r.SortOrder.Value <= maxOrder,
                        "DISPLAY_ORDER_INVALID",
                        $"Thứ tự hiển thị phải là số nguyên từ 1 đến {maxOrder}.",
                        HttpStatusCode.BadRequest);
                    targetOrder = r.SortOrder!.Value;
                }

                if (!r.AllowShift)
                {
                    var conflict = id.HasValue
                        ? targetOrder != existing!.SortOrder && allTypes.Any(x => x.Id != existing.Id && x.SortOrder == targetOrder)
                        : allTypes.Any(x => x.SortOrder == targetOrder);

                    Rule(!conflict,
                        "DISPLAY_ORDER_CONFLICT",
                        $"Thứ tự hiển thị {targetOrder} đã được sử dụng.",
                        HttpStatusCode.Conflict);
                }

                var now = DateTime.UtcNow;
                var entity = existing ?? new CertificateType { CreatedDate = now };
                entity.Name = name;
                entity.Description = description;
                entity.IsActive = r.IsActive;
                entity.IsDeleted = false;
                entity.ModifiedDate = now;
                if (existing is not null)
                    entity.Revision++;

                var ordered = allTypes.Where(x => x.Id != entity.Id).ToList();
                var insertIndex = Math.Clamp(targetOrder - 1, 0, ordered.Count);
                ordered.Insert(insertIndex, entity);

                for (var i = 0; i < ordered.Count; i++)
                {
                    var desiredOrder = i + 1;
                    var item = ordered[i];
                    if (item.SortOrder != desiredOrder)
                    {
                        item.SortOrder = desiredOrder;
                        if (item.Id != entity.Id)
                        {
                            item.Revision++;
                            item.ModifiedDate = now;
                        }
                    }
                }

                if (!id.HasValue)
                    await uow.CertificateTypes.AddAsync(entity, token);

                await uow.SaveChangesAsync(token);
                await RecordAdminAuditAsync("CertificateType", entity.Id, existing is null ? "Created" : "Updated", new
                {
                    entity.Id,
                    entity.Name,
                    entity.SortOrder,
                    entity.IsActive,
                    entity.Revision,
                }, token);
                saved = entity;
                orderToken = ComputeOrderToken(ordered.Select(x => (x.Id, x.SortOrder, x.Revision)));
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Rule(false,
                "ORDER_REVISION_CONFLICT",
                "Dữ liệu chứng chỉ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }

        return new { saved.Id, saved.SortOrder, saved.Revision, OrderToken = orderToken };
    });

    public Task<ApiResponse<object>> ReorderCertificateAsync(int id, ReorderCourseCatalogRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(r.TargetSortOrder >= 1,
            "DISPLAY_ORDER_INVALID",
            "Thứ tự hiển thị phải là số nguyên dương bắt đầu từ 1.",
            HttpStatusCode.BadRequest);

        CertificateType saved = null!;
        string orderToken = string.Empty;

        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                await uow.CertificateTypes.LockOrderScopeAsync(token);

                var allTypes = await uow.CertificateTypes.Query()
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToListAsync(token);

                var entity = allTypes.FirstOrDefault(x => x.Id == id);
                Rule(entity is not null,
                    "CERTIFICATE_NOT_FOUND",
                    "Không tìm thấy chứng chỉ.",
                    HttpStatusCode.NotFound);

                if (r.ExpectedRevision.HasValue)
                {
                    Rule(entity!.Revision == r.ExpectedRevision.Value,
                        "ORDER_REVISION_CONFLICT",
                        "Dữ liệu chứng chỉ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                if (!string.IsNullOrWhiteSpace(r.ExpectedOrderToken))
                {
                    var currentToken = ComputeOrderToken(allTypes.Select(x => (x.Id, x.SortOrder, x.Revision)));
                    Rule(string.Equals(currentToken, r.ExpectedOrderToken, StringComparison.Ordinal),
                        "ORDER_REVISION_CONFLICT",
                        "Thứ tự chứng chỉ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                Rule(r.TargetSortOrder <= allTypes.Count,
                    "DISPLAY_ORDER_INVALID",
                    $"Thứ tự hiển thị phải nằm trong khoảng từ 1 đến {allTypes.Count}.",
                    HttpStatusCode.BadRequest);

                var ordered = allTypes.Where(x => x.Id != id).ToList();
                ordered.Insert(r.TargetSortOrder - 1, entity!);

                var now = DateTime.UtcNow;
                for (var i = 0; i < ordered.Count; i++)
                {
                    var desiredOrder = i + 1;
                    var item = ordered[i];
                    if (item.SortOrder != desiredOrder || item.Id == entity!.Id)
                    {
                        item.SortOrder = desiredOrder;
                        item.Revision++;
                        item.ModifiedDate = now;
                    }
                }

                await uow.SaveChangesAsync(token);
                await RecordAdminAuditAsync("CertificateType", entity!.Id, "Reordered", new
                {
                    entity.Id,
                    entity.Name,
                    entity.SortOrder,
                    entity.Revision,
                }, token);
                saved = entity!;
                orderToken = ComputeOrderToken(ordered.Select(x => (x.Id, x.SortOrder, x.Revision)));
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Rule(false,
                "ORDER_REVISION_CONFLICT",
                "Thứ tự chứng chỉ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }

        return new { saved.Id, saved.SortOrder, saved.Revision, OrderToken = orderToken };
    });

    public Task<ApiResponse<object>> SaveLevelAsync(int? id, SaveCourseLevel r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(r.CertificateTypeId > 0,
            "CERTIFICATE_NOT_FOUND",
            "Không tìm thấy chứng chỉ.",
            HttpStatusCode.NotFound);

        var code = CollapseSpaces(r.Code).ToUpperInvariant();
        Rule(!string.IsNullOrWhiteSpace(code) && code.Length <= CatalogCodeMaxLength,
            "LEVEL_CODE_INVALID",
            $"Mã cấp độ là bắt buộc và tối đa {CatalogCodeMaxLength} ký tự.",
            HttpStatusCode.BadRequest);

        var description = NormalizeDescription(r.Description);
        Rule((description?.Length ?? 0) <= CatalogDescriptionMaxLength,
            "LEVEL_DESCRIPTION_TOO_LONG",
            $"Mô tả cấp độ tối đa {CatalogDescriptionMaxLength} ký tự.",
            HttpStatusCode.BadRequest);

        if (r.SortOrder.HasValue && r.SortOrder.Value < 1)
        {
            Rule(false,
                "DISPLAY_ORDER_INVALID",
                "Thứ tự hiển thị phải là số nguyên dương bắt đầu từ 1.",
                HttpStatusCode.BadRequest);
        }

        CertificateLevel saved = null!;
        string orderToken = string.Empty;

        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                var parentExists = await uow.CertificateTypes.Query()
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == r.CertificateTypeId && !x.IsDeleted, token);
                Rule(parentExists,
                    "CERTIFICATE_NOT_FOUND",
                    "Không tìm thấy chứng chỉ.",
                    HttpStatusCode.NotFound);

                await uow.CertificateLevels.LockOrderScopeAsync(r.CertificateTypeId, token);

                CertificateLevel? existing = null;
                if (id.HasValue)
                {
                    existing = await uow.CertificateLevels.Query()
                        .FirstOrDefaultAsync(x => x.Id == id.Value, token);
                    Rule(existing is not null,
                        "LEVEL_NOT_FOUND",
                        "Không tìm thấy cấp độ.",
                        HttpStatusCode.NotFound);

                    // A renamed level or reassigned parent would corrupt historical lessons, qualifications and courses.
                    Rule(string.Equals(existing!.Code, code, StringComparison.Ordinal) && existing.CertificateTypeId == r.CertificateTypeId,
                        "LEVEL_IDENTITY_LOCKED",
                        "Giữ nguyên mã và chứng chỉ của cấp độ đã tạo. Hãy tạo cấp độ mới nếu nghiệp vụ khác.",
                        HttpStatusCode.Conflict);

                    if (r.ExpectedRevision.HasValue)
                    {
                        Rule(existing.Revision == r.ExpectedRevision.Value,
                            "ORDER_REVISION_CONFLICT",
                            "Dữ liệu cấp độ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                            HttpStatusCode.Conflict);
                    }
                }

                var siblings = await uow.CertificateLevels.Query()
                    .Where(x => x.CertificateTypeId == r.CertificateTypeId)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToListAsync(token);

                Rule(!siblings.Any(x => x.Id != id && string.Equals(CollapseSpaces(x.Code), code, StringComparison.OrdinalIgnoreCase)),
                    "LEVEL_ALREADY_EXISTS",
                    "Mã cấp độ đã tồn tại trong chứng chỉ này.",
                    HttpStatusCode.Conflict);

                if (!string.IsNullOrWhiteSpace(r.ExpectedOrderToken))
                {
                    var currentToken = ComputeOrderToken(siblings.Select(x => (x.Id, x.SortOrder, x.Revision)));
                    Rule(string.Equals(currentToken, r.ExpectedOrderToken, StringComparison.Ordinal),
                        "ORDER_REVISION_CONFLICT",
                        "Thứ tự cấp độ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                var maxOrder = id.HasValue ? Math.Max(1, siblings.Count) : siblings.Count + 1;
                int targetOrder;
                if (!id.HasValue && !r.SortOrder.HasValue)
                {
                    targetOrder = maxOrder;
                }
                else
                {
                    Rule(r.SortOrder.HasValue && r.SortOrder.Value >= 1 && r.SortOrder.Value <= maxOrder,
                        "DISPLAY_ORDER_INVALID",
                        $"Thứ tự hiển thị phải là số nguyên từ 1 đến {maxOrder}.",
                        HttpStatusCode.BadRequest);
                    targetOrder = r.SortOrder!.Value;
                }

                if (!r.AllowShift)
                {
                    var conflict = id.HasValue
                        ? targetOrder != existing!.SortOrder && siblings.Any(x => x.Id != existing.Id && x.SortOrder == targetOrder)
                        : siblings.Any(x => x.SortOrder == targetOrder);

                    Rule(!conflict,
                        "DISPLAY_ORDER_CONFLICT",
                        $"Thứ tự hiển thị {targetOrder} đã được sử dụng trong chứng chỉ này.",
                        HttpStatusCode.Conflict);
                }

                var now = DateTime.UtcNow;
                var entity = existing ?? new CertificateLevel
                {
                    CertificateTypeId = r.CertificateTypeId,
                    Code = code,
                    CreatedDate = now,
                };
                entity.Description = description;
                entity.IsDeleted = !r.IsActive;
                entity.ModifiedDate = now;
                if (existing is not null)
                    entity.Revision++;

                var ordered = siblings.Where(x => x.Id != entity.Id).ToList();
                var insertIndex = Math.Clamp(targetOrder - 1, 0, ordered.Count);
                ordered.Insert(insertIndex, entity);

                for (var i = 0; i < ordered.Count; i++)
                {
                    var desiredOrder = i + 1;
                    var item = ordered[i];
                    if (item.SortOrder != desiredOrder)
                    {
                        item.SortOrder = desiredOrder;
                        if (item.Id != entity.Id)
                        {
                            item.Revision++;
                            item.ModifiedDate = now;
                        }
                    }
                }

                if (!id.HasValue)
                    await uow.CertificateLevels.AddAsync(entity, token);

                await uow.SaveChangesAsync(token);
                await RecordAdminAuditAsync("CertificateLevel", entity.Id, existing is null ? "Created" : "Updated", new
                {
                    entity.Id,
                    entity.CertificateTypeId,
                    entity.Code,
                    entity.SortOrder,
                    IsActive = !entity.IsDeleted,
                    entity.Revision,
                }, token);
                saved = entity;
                orderToken = ComputeOrderToken(ordered.Select(x => (x.Id, x.SortOrder, x.Revision)));
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Rule(false,
                "ORDER_REVISION_CONFLICT",
                "Dữ liệu cấp độ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }

        return new { saved.Id, saved.CertificateTypeId, saved.SortOrder, saved.Revision, OrderToken = orderToken };
    });

    public Task<ApiResponse<object>> ReorderLevelAsync(int id, ReorderCourseCatalogRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(r.TargetSortOrder >= 1,
            "DISPLAY_ORDER_INVALID",
            "Thứ tự hiển thị phải là số nguyên dương bắt đầu từ 1.",
            HttpStatusCode.BadRequest);

        CertificateLevel saved = null!;
        string orderToken = string.Empty;

        try
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                var targetCertId = await uow.CertificateLevels.Query()
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => (int?)x.CertificateTypeId)
                    .FirstOrDefaultAsync(token);

                Rule(targetCertId.HasValue,
                    "LEVEL_NOT_FOUND",
                    "Không tìm thấy cấp độ.",
                    HttpStatusCode.NotFound);

                await uow.CertificateLevels.LockOrderScopeAsync(targetCertId!.Value, token);

                var siblings = await uow.CertificateLevels.Query()
                    .Where(x => x.CertificateTypeId == targetCertId.Value)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToListAsync(token);

                var entity = siblings.FirstOrDefault(x => x.Id == id);
                Rule(entity is not null,
                    "LEVEL_NOT_FOUND",
                    "Không tìm thấy cấp độ.",
                    HttpStatusCode.NotFound);

                if (r.ExpectedRevision.HasValue)
                {
                    Rule(entity!.Revision == r.ExpectedRevision.Value,
                        "ORDER_REVISION_CONFLICT",
                        "Dữ liệu cấp độ vừa được quản trị viên khác cập nhật. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                if (!string.IsNullOrWhiteSpace(r.ExpectedOrderToken))
                {
                    var currentToken = ComputeOrderToken(siblings.Select(x => (x.Id, x.SortOrder, x.Revision)));
                    Rule(string.Equals(currentToken, r.ExpectedOrderToken, StringComparison.Ordinal),
                        "ORDER_REVISION_CONFLICT",
                        "Thứ tự cấp độ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                        HttpStatusCode.Conflict);
                }

                Rule(r.TargetSortOrder <= siblings.Count,
                    "DISPLAY_ORDER_INVALID",
                    $"Thứ tự hiển thị phải nằm trong khoảng từ 1 đến {siblings.Count}.",
                    HttpStatusCode.BadRequest);

                var ordered = siblings.Where(x => x.Id != id).ToList();
                ordered.Insert(r.TargetSortOrder - 1, entity!);

                var now = DateTime.UtcNow;
                for (var i = 0; i < ordered.Count; i++)
                {
                    var desiredOrder = i + 1;
                    var item = ordered[i];
                    if (item.SortOrder != desiredOrder || item.Id == entity!.Id)
                    {
                        item.SortOrder = desiredOrder;
                        item.Revision++;
                        item.ModifiedDate = now;
                    }
                }

                await uow.SaveChangesAsync(token);
                await RecordAdminAuditAsync("CertificateLevel", entity!.Id, "Reordered", new
                {
                    entity.Id,
                    entity.CertificateTypeId,
                    entity.Code,
                    entity.SortOrder,
                    entity.Revision,
                }, token);
                saved = entity!;
                orderToken = ComputeOrderToken(ordered.Select(x => (x.Id, x.SortOrder, x.Revision)));
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Rule(false,
                "ORDER_REVISION_CONFLICT",
                "Thứ tự cấp độ vừa thay đổi bởi quản trị viên khác. Vui lòng tải lại.",
                HttpStatusCode.Conflict);
        }

        return new { saved.Id, saved.CertificateTypeId, saved.SortOrder, saved.Revision, OrderToken = orderToken };
    });
}
