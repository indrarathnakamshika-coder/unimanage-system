using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class AssignmentMaterial
    {
        public int AssignmentMaterialId { get; set; }
        [Required]
        public int AssignmentId { get; set; }
        [Required]
        public string FileName { get; set; }
        [Required]
        public string FilePath { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}