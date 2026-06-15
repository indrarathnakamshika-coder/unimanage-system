using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class Course
    {
        public int CourseId { get; set; }

        [Required]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        public string CourseName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int Credits { get; set; }

        public string Department { get; set; } = string.Empty;

        // 0 means no limit (unlimited)
        public int EnrollmentLimit { get; set; } = 0;
    }
}
