using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace WareHouseNJsound.Models
{
    [Table("Role")]
    public class Role
    {
        [Key]
        public int Role_ID { get; set; }
        public string RoleName { get; set; }

        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
