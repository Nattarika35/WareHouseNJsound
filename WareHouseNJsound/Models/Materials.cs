using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    public class Materials
    {
        [Key]
        public string Materials_ID { get; set; }
        public string MaterialsName { get; set; }
        public int? Category_ID { get; set; }
        public int? Unit_ID { get; set; }
        public int? Type_ID { get; set; }
        public decimal? Price { get; set; }
        public byte[] Picture { get; set; }
        public int? MinimumStock { get; set; }
        public string Description { get; set; }
        public DateTime ReceivedDate { get; set;}
        public DateTime WarrantyExpiryDate { get; set; }


        [ForeignKey("Unit_ID")]
        public virtual Unit Unit { get; set; }

        [ForeignKey("Category_ID")]
        public virtual Category Category { get; set; }

        [ForeignKey("Type_ID")]
        public virtual Type Type { get; set; }

        public virtual Stock Stock { get; set; }
        [NotMapped]                // รับค่าจากฟอร์มไว้ไปลง Stock
        public int? OnHandStock { get; set; }
    }
}
