using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace WareHouseNJsound.Models
{
    [Table("Gender")]
    public class Gender
    {
        [Key]
        public int Gender_ID { get; set; }
        public string GenderName { get; set; }

        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
