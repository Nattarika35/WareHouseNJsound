using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis;

namespace WareHouseNJsound.Models
{
    [Table("Stock")]
    public class Stock
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Stock_ID { get; set; }
        public string Materials_ID { get; set; }
        public int? OnHandStock { get; set; }
        public string Description { get; set; }

        [ForeignKey(nameof(Materials_ID))]
        public virtual Materials Materials { get; set; }
    }
}
