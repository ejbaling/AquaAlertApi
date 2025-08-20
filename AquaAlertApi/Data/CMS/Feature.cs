using System;
using System.ComponentModel.DataAnnotations;

namespace AquaAlertApi.Data.CMS;

public class Feature
{
    [Key]
    public Guid FeatureId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsAddOn { get; set; } = false;

    public bool DefaultEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}
