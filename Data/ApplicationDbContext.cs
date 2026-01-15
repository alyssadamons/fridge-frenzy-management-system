using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Client-facing entities
        public DbSet<Product> Products { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<FridgeRegistration> FridgeRegistrations { get; set; }

        // Shared entities - used by both client and admin
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Employee> Employees { get; set; }

        // System entities
        public DbSet<AppLog> AppLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== CRITICAL: Configure Appointment relationships explicitly =====

            // Customer -> Appointments relationship
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Customer)
                .WithMany(c => c.Appointments) // This tells EF about the navigation property
                .HasForeignKey(a => a.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            // Employee -> Appointments relationship
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.Appointments) // This tells EF about the navigation property
                .HasForeignKey(a => a.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure proper column configurations
            modelBuilder.Entity<Appointment>()
                .Property(a => a.CustomerID)
                .IsRequired();

            modelBuilder.Entity<Appointment>()
                .Property(a => a.EmployeeID)
                .IsRequired(false);
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.ProductId);
                entity.Property(e => e.ProductId)
                      .ValueGeneratedOnAdd(); // This ensures it's an identity column
            });
        }
    }
}