using System;
using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class Message
    {
        public int MessageId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime SentDate { get; set; } = DateTime.Now;
    }
}