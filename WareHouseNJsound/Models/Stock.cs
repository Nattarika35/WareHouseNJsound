using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("Stock")]
    public class Stock
    {
        [Key]
        public int Stock_ID { get; set; }
        public string Materials_ID { get; set; }
        public int OnHandStock { get; set; }
        public string Description { get; set; }
    }
}
