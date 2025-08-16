using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace WareHouseNJsound.Models
{
    [Table("StockRecipt")]
    public class StockRecipt
    {
        [Key]
        public int StockRecipt_ID { get; set; }
        public string Materials_ID { get; set; }
        public int TranType_ID { get; set; }
        public int Quantity { get; set; }
        public DateTime Transection_Date { get; set; }
        public int Unit_ID { get; set; }
        public string Employee_ID { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
    }
}
