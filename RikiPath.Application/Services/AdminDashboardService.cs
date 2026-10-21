using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class AdminDashboardService(IUnitOfWork unitOfWork, IClaimService claims) : IAdminDashboardService
{
    public async Task<ApiResponse<AdminDashboardResponse>> GetAsync(CancellationToken cancellationToken)
    {
        if (claims.GetUserClaim().Role != Role.Admin)
            return ApiResponse<AdminDashboardResponse>.Fail("Chỉ Admin mới được xem tổng quan hệ thống.", HttpStatusCode.Forbidden);
        var now = DateTime.UtcNow;
        // Keep queries sequential: repositories share one DbContext.
        var users = unitOfWork.UserAccounts.Query().Where(x => !x.IsDeleted);
        var totalUsers = await users.CountAsync(cancellationToken);
        var learners = await users.CountAsync(x => x.Role == Role.Learner, cancellationToken);
        var mentors = await users.CountAsync(x => x.Role == Role.Mentor, cancellationToken);
        // ContentApprovalService uses Draft as the reviewable import queue.
        var pendingContent = await unitOfWork.ContentApprovalRequests.Query()
            .CountAsync(x => !x.IsDeleted && x.Status == ContentApprovalStatus.Draft, cancellationToken);
        var pendingMentorSchedules = await unitOfWork.MentorAvailabilities.Query()
            .CountAsync(x => !x.IsDeleted && !x.IsApproved && x.RejectedAt == null && x.StartTime > now, cancellationToken);
        var bookings = await unitOfWork.MentorBookings.Query().CountAsync(x => !x.IsDeleted, cancellationToken);
        var plans = await unitOfWork.SubscriptionPlans.Query().CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);
        var paid = await unitOfWork.UserSubscriptions.Query()
            .CountAsync(x => !x.IsDeleted && x.PaymentStatus == PaymentStatus.Paid, cancellationToken);
        return ApiResponse<AdminDashboardResponse>.Success(new AdminDashboardResponse
        {
            TotalUsers = totalUsers, Learners = learners, Mentors = mentors, PendingContent = pendingContent,
            PendingMentorSchedules = pendingMentorSchedules, MentorBookings = bookings, ActivePlans = plans, PaidSubscriptions = paid
        });
    }
}
