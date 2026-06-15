using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class Assignment
    {
        public int AssignmentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public DateTime Deadline { get; set; }
    }
}