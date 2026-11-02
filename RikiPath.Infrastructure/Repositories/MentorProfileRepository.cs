using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    // // Triển khai repository truy vấn hồ sơ và xác minh chuyên gia.
    public class MentorProfileRepository(AppDbContext context) 
        : GenericRepository<MentorProfile>(context), IMentorProfileRepository
    {
        public Task<MentorProfile?> GetByMentorIdAsync(int mentorId, CancellationToken cancellationToken = default)
            => _context.MentorProfiles
                .FirstOrDefaultAsync(x => x.MentorId == mentorId && !x.IsDeleted, cancellationToken);

        public Task<MentorProfile?> GetWithMentorAsync(int mentorId, CancellationToken cancellationToken = default)
            => _context.MentorProfiles
                .Include(x => x.Mentor)
                .FirstOrDefaultAsync(x => x.MentorId == mentorId && !x.IsDeleted, cancellationToken);

        public async Task<IReadOnlyList<MentorProfile>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default)
            => await _context.MentorProfiles
                .Include(x => x.Mentor)
                .Where(x => x.VerificationStatus == MentorVerificationStatus.Pending && !x.IsDeleted)
                .OrderBy(x => x.SubmittedAt ?? x.CreatedDate)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<MentorProfile>> GetByStatusAsync(MentorVerificationStatus status, CancellationToken cancellationToken = default)
            => await _context.MentorProfiles
                .Include(x => x.Mentor)
                .Where(x => x.VerificationStatus == status && !x.IsDeleted)
                .OrderByDescending(x => x.SubmittedAt ?? x.CreatedDate)
                .ToListAsync(cancellationToken);

        public async Task<(IReadOnlyList<UserAccount> Mentors, int TotalCount)> SearchApprovedMentorsAsync(
            string? searchTerm, string? expertise, string? jlptLevel, int page, int pageSize, string? sortBy, CancellationToken cancellationToken = default)
        {
            var query = _context.Users
                .Include(u => u.MentorProfile)
                .Where(u => u.Role == Role.Mentor && !u.IsDeleted
                    && u.MentorProfile != null
                    && u.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(term)) ||
                    (((u.FirstName ?? "") + " " + (u.LastName ?? "")).ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(expertise))
            {
                var exp = expertise.Trim().ToLower();
                query = query.Where(u => u.MentorProfile != null && u.MentorProfile.Expertise != null && u.MentorProfile.Expertise.ToLower().Contains(exp));
            }

            if (!string.IsNullOrWhiteSpace(jlptLevel))
            {
                var level = jlptLevel.Trim().ToUpper();
                query = query.Where(u => u.MentorProfile != null && u.MentorProfile.JlptLevel != null && u.MentorProfile.JlptLevel.ToUpper().Contains(level));
            }

            query = sortBy?.ToLowerInvariant() switch
            {
                "experience_desc" => query.OrderByDescending(u => u.MentorProfile!.YearsOfExperience).ThenBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),
                "experience_asc" => query.OrderBy(u => u.MentorProfile!.YearsOfExperience).ThenBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),
                "name_asc" => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),
                "name_desc" => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName).ThenBy(u => u.Id),
                _ => query.OrderByDescending(u => u.MentorProfile!.YearsOfExperience).ThenBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id)
            };

            var totalCount = await query.CountAsync(cancellationToken);
            var p = Math.Max(1, page);
            var ps = Math.Clamp(pageSize, 1, 50);

            var items = await query.Skip((p - 1) * ps).Take(ps).ToListAsync(cancellationToken);
            return (items, totalCount);
        }
    }
}
