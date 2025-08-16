using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("Gender")]
    public class Gender
    {
        [Key]
        public int Gender_ID { get; set; }
        public string GenderName { get; set; }
    }
}
