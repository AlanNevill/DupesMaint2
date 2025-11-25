using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;

[PrimaryKey("CheckSumId", "DupBasedOn")]
public partial class CheckSumDupsBasedOn
{
    [Required]
    [StringLength(20)]
    [Unicode(false)]
    public string DupBasedOn { get; set; } = null!;

    [Required]
    public int CheckSumId { get; set; }

    [Required]
    [StringLength(200)]
    [Unicode(false)]
    public string BasedOnVal { get; set; } = null!;

    [ForeignKey("CheckSumId")]
    [InverseProperty("CheckSumDupsBasedOn")]
    public virtual CheckSum CheckSum { get; set; } = null!;
}
