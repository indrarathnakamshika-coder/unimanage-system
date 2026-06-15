using System;

namespace UniManage.Models
{
    public class ConversationListItem
    {
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastDate { get; set; }
        public int UnreadCount { get; set; }
    }
}