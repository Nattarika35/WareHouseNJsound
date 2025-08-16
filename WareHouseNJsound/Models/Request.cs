using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WareHouseNJsound.Models
{
    [Table("Request")]
    public class Request
    {
        [Key]
        public int Request_ID { get; set; }
        public string Employee_ID { get; set; }
        public string Request_Date { get; set; }
        public int Workflow_ID { get; set; }
        public string Description { get; set; }

    }
}
