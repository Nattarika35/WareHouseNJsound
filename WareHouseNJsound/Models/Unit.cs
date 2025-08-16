using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Unit")]
    public class Unit
    {
        [Key]
        public int Unit_ID { get; set; }
        public string UnitName { get; set; }

        public virtual ICollection<Materials> Materials { get; set; }
    }
}
