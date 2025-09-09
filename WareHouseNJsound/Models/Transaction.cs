using System.ComponentModel.DataAnnotations;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.CodeAnalysis;

namespace WareHouseNJsound.Models
{
    [Table("Transaction")]
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Transaction_ID { get; set; }
        public DateTime Transaction_Date { get; set; }
        public int TranType_ID { get; set; }
        public int? Quantity { get; set; }
        public string Materials_ID { get; set; }
        public Guid Request_ID { get; set; }
        public string RequestNumber { get; set; }
        public string Employee_ID { get; set; }
        public string Description { get; set; }


        [ForeignKey(nameof(TranType_ID))]
        public TransactionType transactionTypes { get; set; }
        [ForeignKey(nameof(Materials_ID))]
        public virtual Materials Materials { get; set; }
        [ForeignKey(nameof(Request_ID))]
        public virtual Request Requests { get; set; }
        [ForeignKey(nameof(Employee_ID))]
        public virtual Employee Employee { get; set; }
    }
}
