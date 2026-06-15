using System.Collections.Generic;

namespace UniManage.Models
{
    public class LecturerDashboardViewModel
    {
        public IEnumerable<Course> Courses { get; set; }
        public IEnumerable<Assignment> Assignments { get; set; }
        public IEnumerable<SubmissionSummary> RecentSubmissions { get; set; }
        public IEnumerable<GradingSummary> GradingSummaries { get; set; }
    }

    public class LecturerWorkloadViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public int CoursesCount { get; set; }
        public int MaterialsUploaded { get; set; }
        public int MessagesReceived { get; set; }
    }

    public class StudentDashboardViewModel
    {
        public List<Course> EnrolledCourses { get; set; } = new List<Course>();
        public List<Assignment> UpcomingAssignments { get; set; } = new List<Assignment>();
        public List<SubmissionSummary> Submissions { get; set; } = new List<SubmissionSummary>();
    }
}
