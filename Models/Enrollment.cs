using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class Enrollment
    {
        public int EnrollmentId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CourseId { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.Now;
    }
}