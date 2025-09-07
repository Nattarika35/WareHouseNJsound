using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Notification")]
    public class Notification
    {
        [Key] public int Id { get; set; }

        // แอดมินคนที่จะเห็นแจ้งเตือน (ใช้ Employee_ID ของคุณ)
        [Required] public string Employee_ID { get; set; }

        [Required, MaxLength(200)] public string Title { get; set; }
        [MaxLength(1000)] public string Message { get; set; }
        [MaxLength(500)] public string Link { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        [ForeignKey(nameof(Employee_ID))]
        public virtual Employee Employee { get; set; }
    }
}
