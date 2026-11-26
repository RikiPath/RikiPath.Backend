using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    public Task<ApiResponse<object>> ExamSchedulesAsync(CancellationToken ct) => Run(async () =>
        await uow.JlptExamSchedules.Query().Where(x => Actor.Role == Role.Admin || x.Status == "Approved")
            .OrderBy(x => x.ExamDate).ToListAsync(ct));

    public async Task<ApiResponse<object>> ImportExamSchedulesAsync(CancellationToken ct)
    {
        if (Actor.Role != Role.Admin) return ApiResponse<object>.Fail("Chỉ Quản trị viên được nhập lịch thi.", HttpStatusCode.Forbidden);
        if (examSource is null) return ApiResponse<object>.Fail("Chưa cấu hình nguồn lịch thi.", HttpStatusCode.ServiceUnavailable);
        try
        {
            var imported = await examSource.FetchAsync(ct);
            var count = 0;
            await uow.ExecuteInTransactionAsync(async token =>
            {
                await uow.StudyCourses.LockCohortAsync(-1, token);
                foreach (var item in imported.Where(x => x.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))))
                {
                    var key = $"jlpt:{item.Date:yyyy-MM-dd}";
                    if (await uow.JlptExamSchedules.AnyAsync(x => x.SourceKey == key, token)) continue;
                    await uow.JlptExamSchedules.AddAsync(new() { SourceKey = key, ExamDate = item.Date, SourceUrl = item.SourceUrl }, token);
                    count++;
                }
                await uow.SaveChangesAsync(token);
            }, ct);
            return ApiResponse<object>.Success(new { Imported = count, NeedsAdminReview = true });
        }
        catch (HttpRequestException) { return ApiResponse<object>.Fail("Chưa lấy được lịch JLPT chính thức. Hãy thử lại sau; dữ liệu đã duyệt vẫn giữ nguyên.", HttpStatusCode.BadGateway); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ApiResponse<object>.Fail("Nguồn lịch thi phản hồi quá chậm.", HttpStatusCode.GatewayTimeout); }
    }

    public Task<ApiResponse<object>> ReviewExamScheduleAsync(int id, DateOnly date, string location, bool approve, string note, CancellationToken ct) => Run(async () =>
    {
        Admin();
        var row = await uow.JlptExamSchedules.GetByIdAsync(id, ct);
        Require(row is not null && row.Status == "Draft", "Bản nháp không tồn tại hoặc đã duyệt.");
        Require(date >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) && !string.IsNullOrWhiteSpace(note)
            && note.Length <= 1000 && location.Length <= 200 && (!approve || !string.IsNullOrWhiteSpace(location)),
            "Admin cần kiểm tra ngày thi, địa điểm/đơn vị tổ chức và ghi chú xác nhận.");
        row!.ExamDate = date; row.Location = location.Trim(); row.Status = approve ? "Approved" : "Rejected";
        row.ReviewNote = note.Trim(); row.ReviewedById = Actor.Id; row.ReviewedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct); return new { row.Id };
    });
}
