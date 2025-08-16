using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("TransactionType")]
    public class TransactionType
        {
            [Key]
            public int TranType_ID { get; set; }
            public string TypeName { get; set; }
        }
}
