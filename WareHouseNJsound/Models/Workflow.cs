using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Workflow")]
    public class Workflow
    {
        [Key]
        public int Workflow_ID { get; set; }
        public string WorkflowName { get; set; }
        public int Status_ID { get; set; }
        public string Description { get; set; }
    }
}
