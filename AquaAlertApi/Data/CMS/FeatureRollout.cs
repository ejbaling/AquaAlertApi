using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AquaAlertApi.Data.CMS;

public class FeatureRollout
{
    [Key]
    public Guid RolloutId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid FeatureId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RolloutType { get; set; } = string.Empty; // "Percentage", "Region", "EarlyAdopter"

    [MaxLength(200)]
    public string? Parameter { get; set; } // e.g., "25" or "NZ,AU"

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("Cohort")]
    public Guid? CohortId { get; set; }

    // Navigation
    public virtual Feature Feature { get; set; } = null!;
    public virtual Cohort? Cohort { get; set; }
}
