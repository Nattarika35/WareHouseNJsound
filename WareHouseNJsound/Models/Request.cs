using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;

namespace WareHouseNJsound.Models
{
    [Table("Request")]
    public class Request
    {
        [Key]
        public Guid Request_ID { get; set; }
        public string RequestNumber { get; set; }
        public string Employee_ID { get; set; }
        [ForeignKey(nameof(Employee_ID))]
        public virtual Employee Employee { get; set; }
        public DateTime Request_Date { get; set; }
        public int Status_ID { get; set; }
        public string Description { get; set; }

        [ForeignKey(nameof(Status_ID))]
        public virtual Status Status { get; set; }
        public virtual ICollection<RequestDetail> RequestDetails { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }

    }
}
