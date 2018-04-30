using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Relecloud.Web.Infrastructure;
using Relecloud.Web.Models;
using Relecloud.Web.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Relecloud.Web.Controllers
{
    public class ConcertController : Controller
    {
        #region Fields

        private readonly IConcertRepository concertRepository;
        private readonly IConcertSearchService concertSearchService;
        private readonly IEventSenderService eventSenderService;
        private readonly TelemetryClient telemetryClient = new TelemetryClient();

        #endregion

        #region Constructors

        public ConcertController(IConcertRepository concertRepository, IConcertSearchService concertSearchService, IEventSenderService eventSenderService)
        {
            this.concertRepository = concertRepository;
            this.concertSearchService = concertSearchService;
            this.eventSenderService = eventSenderService;
        }

        #endregion

        #region Index

        public async Task<IActionResult> Index()
        {
            var model = await this.concertRepository.GetUpcomingConcertsAsync(10);
            return View(model);
        }

        #endregion

        #region Details

        public async Task<IActionResult> Details(int id)
        {
            var model = await this.concertRepository.GetConcertByIdAsync(id);
            if (model == null)
            {
                return NotFound();
            }
            return View(model);
        }

        #endregion

        #region Search

        public async Task<IActionResult> Search(SearchRequest request)
        {
            var result = await this.concertSearchService.SearchAsync(request);
            return View(result);
        }

        #endregion

        #region Review

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Review(Review review)
        {
            if (ModelState.IsValid)
            {
                review.UserId = this.User.GetUniqueId();
                review.CreatedTime = DateTimeOffset.UtcNow;
                await this.concertRepository.AddReviewAsync(review);
                await this.eventSenderService.SendEventAsync(Event.ReviewCreated(review.Id));
                this.telemetryClient.TrackEvent("ReviewSubmitted", new Dictionary<string, string> { { "ConcertId", review.ConcertId.ToString() }, { "Description", review.Description }, { "Rating", review.Rating.ToString() } });
            }
            return RedirectToAction(nameof(Details), new { id = review.ConcertId });
        }

        #endregion

        #region Suggest

        public async Task<JsonResult> Suggest(string query)
        {
            var suggestions = await this.concertSearchService.SuggestAsync(query);
            return Json(suggestions);
        }

        #endregion
    }
}