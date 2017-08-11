using Relecloud.Web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Relecloud.Web.Services
{
    public interface ITicketRepository
    {
        void Initialize();
        Task CreateAsync(Ticket ticket);
        Task<IList<Ticket>> GetAllAsync(string userId);
    }
}