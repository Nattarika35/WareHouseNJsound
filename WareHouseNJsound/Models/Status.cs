using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Status")]
    public class Status
    {
        [Key]
        public int? Status_ID { get; set; }
        public string StatusName { get; set; }
        public string Color { get; set; }

        public virtual ICollection<Request> Request { get; set; }
    }
}
