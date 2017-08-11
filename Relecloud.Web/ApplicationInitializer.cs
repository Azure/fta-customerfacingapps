using Relecloud.Web.Services;

namespace Relecloud.Web
{
    public class ApplicationInitializer
    {
        public ApplicationInitializer(
            IConcertRepository concertRepository,
            ITicketRepository ticketRepository,
            IConcertSearchService concertSearchService,
            IEventSenderService eventSenderService)
        {
            // Initialize all resources at application startup.
            concertRepository.Initialize();
            ticketRepository.Initialize();
            concertSearchService.Initialize();
            eventSenderService.Initialize();
        }
    }
}