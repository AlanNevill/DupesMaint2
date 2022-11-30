using DupesMaint2.ModelsTemp;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;
[Index("ChecksumId", Name = "IX_CheckSumDups_CheckSumId", IsUnique = true)]
public partial class CheckSumDups
{
    [Key]
    public int Id { get; set; }

    public int ChecksumId { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string ToDelete { get; set; } = null!;

    public virtual ICollection<CheckSumDupsBasedOn> CheckSumDupsBasedOn { get; } = new List<CheckSumDupsBasedOn>();

    [ForeignKey("ChecksumId")]
    [InverseProperty("CheckSumDups")]
    public virtual CheckSum Checksum { get; set; } = null!;
}
