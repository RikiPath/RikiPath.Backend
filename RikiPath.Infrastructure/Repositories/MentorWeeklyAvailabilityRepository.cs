using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Repositories;
public sealed class MentorWeeklyAvailabilityRepository(AppDbContext context)
    : GenericRepository<MentorWeeklyAvailability>(context), IMentorWeeklyAvailabilityRepository { }
