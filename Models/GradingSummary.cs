namespace UniManage.Models
{
    public class GradingSummary
    {
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; }
        public int TotalSubmissions { get; set; }
        public int GradedCount { get; set; }
        public int UngradedCount { get; set; }
    }
}
