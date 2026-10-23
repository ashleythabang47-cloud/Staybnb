using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers
{
    // Database-backed messaging (not real-time): an inbox to read and reply
    [Authorize]
    public class MessagesController : Controller
    {
        private const int MaxLength = 2000;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _log;
        private readonly INotificationService _notifications;

        public MessagesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IActivityLogService log,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
            _notifications = notifications;
        }

        // ---------- Inbox: one row per person ----------

        public async Task<IActionResult> Index()
        {
            var me = _userManager.GetUserId(User)!;

            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == me || m.ReceiverId == me)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var last = g.First(); // newest, because of the ordering above
                    return new ConversationSummaryViewModel
                    {
                        OtherUser = (last.SenderId == me ? last.Receiver : last.Sender)!,
                        LastMessage = last,
                        UnreadCount = g.Count(m => m.ReceiverId == me && !m.IsRead)
                    };
                })
                .OrderByDescending(c => c.LastMessage.Timestamp)
                .ToList();

            return View(conversations);
        }

        // ---------- One conversation (opening it marks received messages as read) ----------

        [HttpGet]
        public async Task<IActionResult> Conversation(string id, int? propertyId)
        {
            var me = _userManager.GetUserId(User)!;

            if (id == me)
                return RedirectToAction(nameof(Index));

            var other = await _userManager.FindByIdAsync(id);
            if (other == null)
                return NotFound();

            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Where(m => (m.SenderId == me && m.ReceiverId == id) ||
                            (m.SenderId == id && m.ReceiverId == me))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            if (!messages.Any() && !await CanMessageAsync(me, other, propertyId))
                return Forbid();

            // Read / unread is saved in the database
            var unread = messages.Where(m => m.ReceiverId == me && !m.IsRead).ToList();
            if (unread.Any())
            {
                foreach (var m in unread)
                    m.IsRead = true;
                await _db.SaveChangesAsync();
            }

            var model = new ConversationViewModel
            {
                CurrentUserId = me,
                OtherUser = other,
                Messages = messages,
                PropertyId = propertyId
            };

            // First message about a listing: suggest an opening line
            if (!messages.Any() && propertyId.HasValue)
            {
                var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId.Value);
                if (property != null)
                    model.Draft = $"Hi, I have a question about '{property.Title}'.";
            }

            return View(model);
        }

        // ---------- Send (new message or reply) ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(string toUserId, string? content, int? propertyId)
        {
            var me = _userManager.GetUserId(User)!;

            if (toUserId == me)
                return RedirectToAction(nameof(Index));

            var other = await _userManager.FindByIdAsync(toUserId);
            if (other == null)
                return NotFound();

            content = content?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                TempData["Error"] = "Please write a message first.";
                return RedirectToAction(nameof(Conversation), new { id = toUserId, propertyId });
            }
            if (content.Length > MaxLength)
            {
                TempData["Error"] = $"Messages can be at most {MaxLength} characters.";
                return RedirectToAction(nameof(Conversation), new { id = toUserId, propertyId });
            }

            if (!await CanMessageAsync(me, other, propertyId))
                return Forbid();

            _db.Messages.Add(new Message
            {
                SenderId = me,
                ReceiverId = toUserId,
                Content = content,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync();

            var sender = await _userManager.GetUserAsync(User);
            var preview = content.Length > 80 ? content.Substring(0, 80) + "..." : content;

            await _log.LogAsync(me, $"Message sent to {other.Email}", ActivityType.Message);
            await _notifications.NotifyAsync(toUserId, "New Message",
                $"{sender?.FullName} sent you a message: {preview}", NotificationType.Message);

            return RedirectToAction(nameof(Conversation), new { id = toUserId });
        }

        // ---------- Who may message whom ----------

        private async Task<bool> CanMessageAsync(string me, ApplicationUser other, int? propertyId)
        {
            // 1. Contacting the host of a listing
            if (propertyId.HasValue &&
                await _db.Properties.AnyAsync(p => p.Id == propertyId.Value && p.HostId == other.Id))
                return true;

            // 2. Replying to someone who already wrote to you (or you wrote to)
            if (await _db.Messages.AnyAsync(m =>
                (m.SenderId == me && m.ReceiverId == other.Id) ||
                (m.SenderId == other.Id && m.ReceiverId == me)))
                return true;

            // 3. A guest and the host of a property they booked
            if (await _db.Bookings.AnyAsync(b =>
                (b.GuestId == me && b.Property!.HostId == other.Id) ||
                (b.GuestId == other.Id && b.Property!.HostId == me)))
                return true;

            // 4. Admins can message anyone, and anyone can message an admin
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return true;

            if (await _userManager.IsInRoleAsync(other, "Admin") ||
                await _userManager.IsInRoleAsync(other, "SuperAdmin"))
                return true;

            return false;
        }
    }
}
