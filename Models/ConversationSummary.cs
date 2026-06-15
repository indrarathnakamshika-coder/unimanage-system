namespace UniManage.Models
{
    public class ConversationSummary
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int LecturerId { get; set; }
        public string LecturerName { get; set; }
        public DateTime LastMessageDate { get; set; }
        public string LastMessageSnippet { get; set; }
    }
}