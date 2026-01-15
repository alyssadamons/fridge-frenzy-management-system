// Models/AppLog.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class AppLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } // "UserCreated", "CustomerAdded", "InfoChanged", etc.

        [Required]
        public string Description { get; set; } // Detailed description

        [MaxLength(256)]
        public string UserEmail { get; set; } // Who performed the action

        [MaxLength(256)]
        public string AffectedUserEmail { get; set; } // Who was affected (if different)

        [MaxLength(100)]
        public string AffectedUserName { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;


        public string AdditionalData { get; set; } // JSON or additional info
    }
}