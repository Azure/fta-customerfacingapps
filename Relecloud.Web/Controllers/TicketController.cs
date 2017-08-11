using Microsoft.AspNetCore.Mvc;
using Relecloud.Web.Models;
using Relecloud.Web.Services;
using System.Threading.Tasks;

namespace Relecloud.Web.Controllers
{
    public class TicketController : Controller
    {
        private const string userId = "1";

        #region Fields

        private readonly IConcertRepository concertRepository;
        private readonly ITicketRepository ticketRepository;

        #endregion

        #region Constructors

        public TicketController(IConcertRepository concertRepository, ITicketRepository ticketRepository)
        {
            this.concertRepository = concertRepository;
            this.ticketRepository = ticketRepository;
        }

        #endregion

        #region Index

        public async Task<IActionResult> Index()
        {
            var model = await this.ticketRepository.GetAllAsync(userId);
            return View(model);
        }

        #endregion

        #region Buy

        public async Task<IActionResult> Buy(int concertId)
        {
            var model = await this.concertRepository.GetByIdAsync(concertId);
            if (model == null)
            {
                return NotFound();
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName(nameof(Buy))]
        public async Task<IActionResult> BuyConfirmed(int concertId)
        {
            if (ModelState.IsValid)
            {
                var concert = await this.concertRepository.GetByIdAsync(concertId);
                if (concert == null)
                {
                    return BadRequest();
                }
                var ticket = new Ticket
                {
                    ConcertId = concertId,
                    UserId = userId,
                    Description = $"{concert.Artist} on {concert.StartTime.UtcDateTime.ToString()}",
                    Price = concert.Price
                };
                await this.ticketRepository.CreateAsync(ticket);
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}