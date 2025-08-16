using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Category")]
    public class Category
    {
        [Key]
        public int? Category_ID { get; set; }
        public string CategoryName { get; set; }

        public virtual ICollection<Materials> Materials { get; set; }
    }
}
