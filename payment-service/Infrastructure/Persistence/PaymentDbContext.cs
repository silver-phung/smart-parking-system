using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

        // Khai báo 4 bảng sẽ tạo trên Neon
        public DbSet<PricingPolicy> PricingPolicies { get; set; }
        public DbSet<ParkingSessionBilling> ParkingSessions { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình kiểu dữ liệu tiền tệ (decimal) cho PostgreSQL trên Neon để tránh cảnh báo
            modelBuilder.Entity<PricingPolicy>()
                .Property(p => p.DayBlockRate).HasColumnType("numeric(18,2)");
            modelBuilder.Entity<PricingPolicy>()
                .Property(p => p.EveningBlockRate).HasColumnType("numeric(18,2)");
            modelBuilder.Entity<PricingPolicy>()
                .Property(p => p.OvernightTouchRate).HasColumnType("numeric(18,2)");
                
            modelBuilder.Entity<Invoice>()
                .Property(i => i.TotalAmount).HasColumnType("numeric(18,2)");
                
            modelBuilder.Entity<PaymentTransaction>()
                .Property(pt => pt.Amount).HasColumnType("numeric(18,2)");
        }
    }
}