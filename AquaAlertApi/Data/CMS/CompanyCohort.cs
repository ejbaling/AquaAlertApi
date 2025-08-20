
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AquaAlertApi.Data.CMS;

public class CompanyCohort
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    [ForeignKey("Company")]
    public Guid CompanyId { get; set; }
     [Required]
    [ForeignKey("Cohort")]
    public Guid CohortId { get; set; }

    // Navigation properties
    public virtual Company Company { get; set; } = null!;
    public virtual Cohort Cohort { get; set; } = null!;
}
