using System;
using System.Collections.Generic;

namespace AquaAlertApi.Data;

public partial class Tank
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal CapacityLiters { get; set; }

    public decimal HeightCm { get; set; }

    public string? Location { get; set; }

    public virtual ICollection<WaterLevelLog> WaterLevelLogs { get; set; } = new List<WaterLevelLog>();
}
