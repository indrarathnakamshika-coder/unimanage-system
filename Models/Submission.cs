using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class Submission
    {
        public int SubmissionId { get; set; }

        [Required]
        public int AssignmentId { get; set; }

        public int StudentId { get; set; }

        [Required]
        public int UserId { get; set; }

        public string SubmissionText { get; set; }

        public string FilePath { get; set; }

        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        public string? Grade { get; set; }

        // Lecturer feedback for the student
        public string? Feedback { get; set; }
    }
}
