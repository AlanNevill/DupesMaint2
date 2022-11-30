using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;

[PrimaryKey("CheckSumId", "DupBasedOn")]
public partial class CheckSumDupsBasedOn
{
    [Key]
    public int CheckSumId { get; set; }

    [Key]
    [StringLength(20)]
    [Unicode(false)]
    public string DupBasedOn { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string BasedOnVal { get; set; } = null!;

    public virtual CheckSumDups CheckSum { get; set; } = null!;
}
