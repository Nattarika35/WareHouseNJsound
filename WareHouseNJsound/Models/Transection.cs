using System.ComponentModel.DataAnnotations;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Transection")]
    public class Transection
    {
        [Key]
        public int Transection_ID { get; set; }
        public DateTime Transection_Date { get; set; }
        public int TranType_ID { get; set; }
        public int Quantity { get; set; }
        public string Materials_ID { get; set; }
        public int Request_ID { get; set; }
        public string Employee_ID { get; set; }
        public string Description { get; set; }
    }
}
