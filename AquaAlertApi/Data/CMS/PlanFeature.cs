using System.ComponentModel.DataAnnotations;

namespace AquaAlertApi.Data.CMS;

public partial class PlanFeature
{
    [Key]
    public Guid PlanFeatureId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public Guid FeatureId { get; set; }

    public bool Enabled { get; set; } = true;

    // Navigation
    public virtual Plan Plan { get; set; } = null!;
    public virtual Feature Feature { get; set; } = null!;
}
