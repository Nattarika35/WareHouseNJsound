using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Type")]
    public class Type
    {
        [Key]     
        public int? Type_ID { get; set; }
        public string TypeName { get; set; }

        public virtual ICollection<Materials> Materials { get; set; }
    }
}
