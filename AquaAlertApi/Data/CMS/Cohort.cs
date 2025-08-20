using System;
using System.ComponentModel.DataAnnotations;

namespace AquaAlertApi.Data.CMS;

public class Cohort
{
    [Key]
    public Guid CohortId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Alpha", "Beta"

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: companies in this cohort
    public virtual ICollection<CompanyCohort> CompanyCohorts { get; set; } = new List<CompanyCohort>();
}
