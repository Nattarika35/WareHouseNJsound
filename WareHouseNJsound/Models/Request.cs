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
        public DateTime Request_Date { get; set; }
        public int Workflow_ID { get; set; }
        public string Description { get; set; }


        public virtual ICollection<RequestDetail> RequestDetails { get; set; }
    }
}
