using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("Jobs")]
    public class Jobs
    {
        [Key]
        public int JobsID { get; set; }
        public string JobsName { get; set; }
    }
}
