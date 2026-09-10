using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories;

public class CoursePurchaseRepository : GenericRepository<CoursePurchase>, ICoursePurchaseRepository
{
    public CoursePurchaseRepository(AppDbContext context) : base(context)
    {
    }

    private IQueryable<CoursePurchase> BuildSearchQuery(int? userId, int? courseId, PaymentStatus? status)
    {
        IQueryable<CoursePurchase> query = _context.CoursePurchases.AsQueryable();

        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (courseId.HasValue) query = query.Where(x => x.CourseId == courseId.Value);
        if (status.HasValue) query = query.Where(x => x.PaymentStatus == status.Value);

        return query;
    }

    public async Task<List<CoursePurchase>> SearchAsync(int? userId, int? courseId,
        PaymentStatus? status, int pageIndex, int pageSize)
        => await BuildSearchQuery(userId, courseId, status)
            .Include(x => x.Course)
            .Include(x => x.UserAccount)
            .OrderByDescending(x => x.PurchasedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> CountSearchAsync(int? userId, int? courseId, PaymentStatus? status)
        => await BuildSearchQuery(userId, courseId, status).CountAsync();

    public async Task<CoursePurchase?> GetByUserAndCourseAsync(int userId, int courseId)
        => await _context.CoursePurchases
            .Include(x => x.Course)
            .Where(x => x.UserId == userId && x.CourseId == courseId)
            .OrderByDescending(x => x.PurchasedAt)
            .FirstOrDefaultAsync();

    public async Task<CoursePurchase?> GetByOrderCodeAsync(long orderCode)
        => await _context.CoursePurchases
            .Include(x => x.Course)
            .Include(x => x.UserAccount)
            .FirstOrDefaultAsync(x => x.OrderCode == orderCode);

    public async Task<bool> HasPurchasedCourseAsync(int userId, int courseId)
        => await _context.CoursePurchases
            .AnyAsync(x => x.UserId == userId
                && x.CourseId == courseId
                && x.PaymentStatus == PaymentStatus.Paid);

    public async Task<List<CoursePurchase>> GetPurchasedCoursesByUserAsync(int userId)
        => await _context.CoursePurchases
            .Include(x => x.Course)
            .Where(x => x.UserId == userId && x.PaymentStatus == PaymentStatus.Paid)
            .OrderByDescending(x => x.PurchasedAt)
            .ToListAsync();
}