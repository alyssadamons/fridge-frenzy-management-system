using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Dashboard.Data
{
    public class DashboardDbContext : DbContext
    {
        public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options) { }

        //public DbSet<Customer> Customers { get; set; }
        //public DbSet<Employee> Employees { get; set; }
        //public DbSet<Appointment> Appointments { get; set; }

        
    }
}