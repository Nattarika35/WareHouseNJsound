using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace WareHouseNJsound.Models
{
    [Table("RequestDetail")]
    public class RequestDetail
    {
        [Key]
        public Guid RequestDetail_ID { get; set; }
        public Guid Request_ID { get; set; }
        public string Materials_ID { get; set; } 
        public int Quantity { get; set; }
        public int MiscIssue { get; set; }
        public int Unit_ID { get; set; }
        public int Jobs_ID { get; set; }

        [ForeignKey("Request_ID")]
        public virtual Request Request { get; set; }

        [ForeignKey("Materials_ID")]
        public virtual Materials Materials { get; set; }

        [ForeignKey("Unit_ID")]
        public virtual Unit Unit { get; set; }

        [ForeignKey("Jobs_ID")]
        public virtual Jobs Jobs { get; set; }
    }
}
