using System.ComponentModel.DataAnnotations;

namespace AquaAlertApi.Data.CMS;

public partial class Company
{
    [Key]
    public Guid CompanyId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid PlanId { get; set; }   // FK to Plans

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Plan Plan { get; set; } = null!;
    public virtual ICollection<CompanyCohort> CompanyCohorts { get; set; } = new List<CompanyCohort>();
}
