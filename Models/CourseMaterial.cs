using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class CourseMaterial
    {
        public int CourseMaterialId { get; set; }
        [Required]
        public int CourseId { get; set; }
        [Required]
        public string FileName { get; set; }
        [Required]
        public string FilePath { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string Description { get; set; }
    }
}