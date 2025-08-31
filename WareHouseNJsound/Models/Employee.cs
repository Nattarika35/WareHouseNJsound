using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseNJsound.Models
{
    [Table("Employee")]
    public class Employee
    {
        [Key]
        public string Employee_ID { get; set; }
        public byte[] Picture { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Emp_Fname { get; set; }
        public string Emp_Lname { get; set; }
        public string Emp_Tel { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public DateTime? Brithdate { get; set; }
        public int? Gender_ID { get; set; }
        public int? Role_ID { get; set; }
        public string Personal_ID { get; set; }
        [NotMapped]
        public string FullName
        {
            get
            {
                return Emp_Fname + " " + Emp_Lname;
            }
        }
    }
}
