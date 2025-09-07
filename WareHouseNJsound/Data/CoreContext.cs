using Microsoft.EntityFrameworkCore;
using WareHouseNJsound.Models;

namespace WareHouseNJsound.Data
{
    public class CoreContext : DbContext
    {
        public CoreContext(DbContextOptions<CoreContext> options)
           : base(options)
        {
        }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Materials> materials { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<RequestDetail> RequestDetails { get; set; }
        public DbSet<Jobs> Jobs { get; set; }
        public DbSet<Gender> genders { get; set; }
        public DbSet<Role> roles { get; set; }
        public DbSet<Status> status { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<StockRecipt> StockRecipts { get; set; }
        public DbSet<TransactionType> transactionTypes { get; set; }
        public DbSet<Transection> transections { get; set; }
        public DbSet<Workflow> workflows { get; set; }
        public DbSet<Notification> Notifications { get; set; }

    }
}
