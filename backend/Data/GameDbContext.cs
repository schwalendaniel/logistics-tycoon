using LogisticsGame.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsGame.Api.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<ActiveTourRecord> Tours => Set<ActiveTourRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().HasKey(c => c.Id);
        modelBuilder.Entity<Truck>().HasKey(t => t.Id);
        modelBuilder.Entity<Driver>().HasKey(d => d.Id);
        modelBuilder.Entity<Job>().HasKey(j => j.Id);
        modelBuilder.Entity<Depot>().HasKey(d => d.Id);
        modelBuilder.Entity<LedgerEntry>().HasKey(l => l.Id);
        modelBuilder.Entity<ActiveTourRecord>().HasKey(t => t.Id);

        modelBuilder.Entity<Truck>().Property(t => t.PurchasePrice).HasConversion<double>();
        modelBuilder.Entity<Driver>().Property(d => d.MonthlySalary).HasConversion<double>();
        modelBuilder.Entity<Driver>().Property(d => d.CurrentSalary).HasConversion<double>();
        modelBuilder.Entity<Driver>().Property(d => d.ExpectedSalary).HasConversion<double>();
        modelBuilder.Entity<Job>().Property(j => j.Revenue).HasConversion<double>();
        modelBuilder.Entity<Job>().Property(j => j.PenaltyFine).HasConversion<double>();
        modelBuilder.Entity<Company>().Property(c => c.Balance).HasConversion<double>();
        modelBuilder.Entity<LedgerEntry>().Property(l => l.Amount).HasConversion<double>();
        modelBuilder.Entity<ActiveTourRecord>().Property(t => t.AccumulatedFuelCost).HasConversion<double>();
        modelBuilder.Entity<ActiveTourRecord>().Property(t => t.AccumulatedToll).HasConversion<double>();
        modelBuilder.Entity<ActiveTourRecord>().Property(t => t.AccumulatedWear).HasConversion<double>();
    }
}
