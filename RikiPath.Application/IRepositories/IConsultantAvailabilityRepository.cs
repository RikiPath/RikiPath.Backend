using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IConsultantAvailabilityRepository : IGenericRepository<ConsultantAvailability>
    {
        Task<List<ConsultantAvailability>> GetByConsultantAsync(int consultantId);
        Task<List<ConsultantAvailability>> GetAvailableSlotsAsync(int? consultantId, DateTime from, DateTime to);
    }
}
