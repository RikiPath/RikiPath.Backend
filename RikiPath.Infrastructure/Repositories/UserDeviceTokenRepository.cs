using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class UserDeviceTokenRepository : GenericRepository<UserDeviceToken>, IUserDeviceTokenRepository
    {
        public UserDeviceTokenRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<UserDeviceToken>> GetActiveTokensByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserDeviceTokens
                .Where(t => t.UserId == userId && t.IsActive)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserDeviceToken>> GetActiveTokensByUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
        {
            var idList = userIds.Distinct().ToList();
            return await _context.UserDeviceTokens
                .Where(t => idList.Contains(t.UserId) && t.IsActive)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserDeviceToken?> GetByTokenAsync(string fcmToken, CancellationToken cancellationToken = default)
        {
            return await _context.UserDeviceTokens
                .FirstOrDefaultAsync(t => t.FcmToken == fcmToken, cancellationToken);
        }

        public async Task<UserDeviceToken?> GetByUserAndDeviceAsync(int userId, string deviceIdentifier, CancellationToken cancellationToken = default)
        {
            return await _context.UserDeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceIdentifier == deviceIdentifier, cancellationToken);
        }

        public async Task DeactivateTokenAsync(string fcmToken, CancellationToken cancellationToken = default)
        {
            var tokens = await _context.UserDeviceTokens
                .Where(t => t.FcmToken == fcmToken && t.IsActive)
                .ToListAsync(cancellationToken);

            if (tokens.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var t in tokens)
                {
                    t.IsActive = false;
                    t.UpdatedAtUtc = now;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task DeactivateDeviceAsync(int userId, string deviceIdentifier, CancellationToken cancellationToken = default)
        {
            var tokens = await _context.UserDeviceTokens
                .Where(t => t.UserId == userId && t.DeviceIdentifier == deviceIdentifier && t.IsActive)
                .ToListAsync(cancellationToken);

            if (tokens.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var t in tokens)
                {
                    t.IsActive = false;
                    t.UpdatedAtUtc = now;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<UserDeviceToken> RegisterOrUpdateTokenAsync(
            int userId,
            string deviceIdentifier,
            string platform,
            string fcmToken,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            // 1. Cross-user isolation:
            // If this token currently belongs to any other user, deactivate it immediately.
            var otherUserTokens = await _context.UserDeviceTokens
                .Where(t => t.FcmToken == fcmToken && t.UserId != userId && t.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in otherUserTokens)
            {
                other.IsActive = false;
                other.UpdatedAtUtc = now;
            }

            // 2. Check if (UserId, DeviceIdentifier) already exists
            var existing = await _context.UserDeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceIdentifier == deviceIdentifier, cancellationToken);

            if (existing != null)
            {
                existing.FcmToken = fcmToken;
                existing.Platform = platform;
                existing.IsActive = true;
                existing.UpdatedAtUtc = now;
                existing.LastSeenAtUtc = now;
                await _context.SaveChangesAsync(cancellationToken);
                return existing;
            }

            // 3. Check if (UserId, FcmToken) exists under a different deviceIdentifier
            var tokenMatch = await _context.UserDeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.FcmToken == fcmToken, cancellationToken);

            if (tokenMatch != null)
            {
                tokenMatch.DeviceIdentifier = deviceIdentifier;
                tokenMatch.Platform = platform;
                tokenMatch.IsActive = true;
                tokenMatch.UpdatedAtUtc = now;
                tokenMatch.LastSeenAtUtc = now;
                await _context.SaveChangesAsync(cancellationToken);
                return tokenMatch;
            }

            // 4. Create new device token
            var newToken = new UserDeviceToken
            {
                UserId = userId,
                DeviceIdentifier = deviceIdentifier,
                Platform = platform,
                FcmToken = fcmToken,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastSeenAtUtc = now,
                CreatedDate = now,
            };

            await _context.UserDeviceTokens.AddAsync(newToken, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return newToken;
        }
    }
}
