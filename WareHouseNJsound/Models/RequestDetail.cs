using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("RequestDetail")]
    public class RequestDetail
    {
        [Key]
        public int RequestDetail_ID { get; set; }
        public int Materials_ID { get; set; } 
        public string Quantity { get; set; }
        public int Misclssue { get; set; }
        public int Unit_ID { get; set; }
        public int Jobs_ID { get; set; }

    }
}
