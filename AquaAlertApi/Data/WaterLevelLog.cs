using System;
using System.Collections.Generic;

namespace AquaAlertApi.Data;

public partial class WaterLevelLog
{
    public int Id { get; set; }

    public int TankId { get; set; }

    public decimal WaterLevelCm { get; set; }

    public decimal? WaterVolumeLiters { get; set; }

    public decimal? PercentageFull { get; set; }

    public decimal? TemperatureC { get; set; }

    public string? SensorStatus { get; set; }

    public decimal? BatteryVoltage { get; set; }

    public DateTime LoggedAt { get; set; }

    public virtual Tank Tank { get; set; } = null!;

    public decimal? Temperature { get; set; }
}
