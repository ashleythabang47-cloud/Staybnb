using Staybnb.Models;

namespace Staybnb.ViewModels
{
    // One row of the inbox: the latest message with one other person
    public class ConversationSummaryViewModel
    {
        public ApplicationUser OtherUser { get; set; } = null!;
        public Message LastMessage { get; set; } = null!;
        public int UnreadCount { get; set; }
    }

    // A full conversation with one other person
    public class ConversationViewModel
    {
        public string CurrentUserId { get; set; } = string.Empty;
        public ApplicationUser OtherUser { get; set; } = null!;
        public List<Message> Messages { get; set; } = new();
        public int? PropertyId { get; set; }
        public string Draft { get; set; } = string.Empty;
    }
}
