using back.Data;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace back.Hubs
{
    public class QuizHub: Hub
    {
        private readonly ApplicationDbContext _context;

        public QuizHub(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task UpdateVisit()
        {
            _context.Visits.FirstOrDefault()!.NumberOfVisits++;
            _context.SaveChanges();
            await Clients.All.SendAsync("VisitUpdated", new { update = _context.Visits.FirstOrDefault()!.NumberOfVisits });
        }
    }
}
