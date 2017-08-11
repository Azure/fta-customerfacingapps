using Relecloud.Web.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Relecloud.Web.Services
{
    public interface IConcertRepository : IDisposable
    {
        void Initialize();
        Task<Concert> GetByIdAsync(int id);
        Task<IList<Concert>> GetUpcomingConcertsAsync(int count);
        Task AddReviewAsync(Review review);
    }
}