using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AquaAlertApi.Data;


public partial class AppDbContext : DbContext
{
    private readonly IConfiguration _configuration;

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    public virtual DbSet<Tank> Tanks { get; set; }

    public virtual DbSet<WaterLevelLog> WaterLevelLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(_configuration.GetConnectionString("Postgres"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tank>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tanks_pkey");

            entity.ToTable("tanks");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CapacityLiters)
                .HasPrecision(10, 2)
                .HasColumnName("capacity_liters");
            entity.Property(e => e.HeightCm)
                .HasPrecision(6, 2)
                .HasColumnName("height_cm");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<WaterLevelLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("water_level_logs_pkey");

            entity.ToTable("water_level_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BatteryVoltage)
                .HasPrecision(5, 2)
                .HasColumnName("battery_voltage");
            entity.Property(e => e.LoggedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("logged_at");
            entity.Property(e => e.PercentageFull)
                .HasPrecision(5, 2)
                .HasColumnName("percentage_full");
            entity.Property(e => e.SensorStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'OK'::character varying")
                .HasColumnName("sensor_status");
            entity.Property(e => e.TankId).HasColumnName("tank_id");
            entity.Property(e => e.TemperatureC)
                .HasPrecision(5, 2)
                .HasColumnName("temperature_c");
            entity.Property(e => e.WaterLevelCm)
                .HasPrecision(6, 2)
                .HasColumnName("water_level_cm");
            entity.Property(e => e.WaterVolumeLiters)
                .HasPrecision(10, 2)
                .HasColumnName("water_volume_liters");

            entity.HasOne(d => d.Tank).WithMany(p => p.WaterLevelLogs)
                .HasForeignKey(d => d.TankId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_tank");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
