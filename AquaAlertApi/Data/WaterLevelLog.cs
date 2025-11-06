using System;
using System.Collections.Generic;

namespace AquaAlertApi.Data;

public partial class WaterLevelLog
{
    public int Id { get; set; }

    public string? ClientId { get; set; }

    public int TankId { get; set; }

    public decimal WaterLevelCm { get; set; }

    public decimal? WaterVolumeLiters { get; set; }

    public decimal? PercentageFull { get; set; }

    public decimal? TemperatureC { get; set; }

    public string? SensorStatus { get; set; }

    public decimal? BatteryVoltage { get; set; }

    // change to DateTimeOffset
    public DateTimeOffset LoggedAt { get; set; }

    // store local time as DateTime (no offset) for Asia/Manila
    public DateTime? LoggedAtLocal { get; set; }

    public virtual Tank Tank { get; set; } = null!;

    public decimal? Temperature { get; set; }
}
