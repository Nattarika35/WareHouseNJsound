using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("Role")]
    public class Role
    {
        [Key]
        public int Role_ID { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }
}
