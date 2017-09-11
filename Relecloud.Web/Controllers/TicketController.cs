using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Relecloud.Web.Infrastructure;
using Relecloud.Web.Models;
using Relecloud.Web.Services;
using System.Threading.Tasks;

namespace Relecloud.Web.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        #region Fields

        private readonly IConcertRepository concertRepository;
        private readonly IEventSenderService eventSenderService;

        #endregion

        #region Constructors

        public TicketController(IConcertRepository concertRepository, IEventSenderService eventSenderService)
        {
            this.concertRepository = concertRepository;
            this.eventSenderService = eventSenderService;
        }

        #endregion

        #region Index

        public async Task<IActionResult> Index()
        {
            var userId = this.User.GetUniqueId();
            var model = await this.concertRepository.GetAllTicketsAsync(userId);
            return View(model);
        }

        #endregion

        #region Buy

        public async Task<IActionResult> Buy(int concertId)
        {
            var model = await this.concertRepository.GetConcertByIdAsync(concertId);
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
                var concert = await this.concertRepository.GetConcertByIdAsync(concertId);
                if (concert == null)
                {
                    return BadRequest();
                }
                var ticket = new Ticket
                {
                    ConcertId = concertId,
                    UserId = this.User.GetUniqueId()
                };
                await this.concertRepository.CreateTicketAsync(ticket);
                await this.eventSenderService.SendEventAsync(Event.TicketCreated(ticket.Id));
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}