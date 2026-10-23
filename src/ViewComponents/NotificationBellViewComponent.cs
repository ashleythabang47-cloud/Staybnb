using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.ViewComponents
{
    // Navbar icons: the bell (unread notifications) and the envelope (unread messages)
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new NavCountsViewModel();
            var userId = _userManager.GetUserId(UserClaimsPrincipal);

            if (userId != null)
            {
                model.UnreadNotifications = await _db.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead);
                model.UnreadMessages = await _db.Messages
                    .CountAsync(m => m.ReceiverId == userId && !m.IsRead);
            }

            return View(model);
        }
    }
}
