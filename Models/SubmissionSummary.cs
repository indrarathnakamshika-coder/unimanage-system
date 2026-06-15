using System;

namespace UniManage.Models
{
    public class SubmissionSummary
    {
        public int SubmissionId { get; set; }
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public DateTime SubmissionDate { get; set; }
        public string Grade { get; set; }
        public string Feedback { get; set; }
    }
}
