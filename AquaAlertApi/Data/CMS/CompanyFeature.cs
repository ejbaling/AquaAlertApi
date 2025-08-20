using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AquaAlertApi.Data.CMS;

public class CompanyFeature
{
    [Key]
    public Guid CompanyFeatureId { get; set; } = Guid.NewGuid();

    [Required]
    [ForeignKey("Company")]
    public Guid CompanyId { get; set; }

    [Required]
    [ForeignKey("Feature")]
    public Guid FeatureId { get; set; }

    [Required]
    public bool Enabled { get; set; }

    [MaxLength(50)]
    public string? Source { get; set; } // e.g., "Plan", "AddOn", "BetaProgram", "Override"

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Company Company { get; set; } = null!;
    public virtual Feature Feature { get; set; } = null!;
}
