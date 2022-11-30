using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace DupesMaint2.Models;

public partial class CheckSumDups
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int CheckSumId { get; set; }
    public string ToDelete { get; set; } = "N";

    // Navigation property
    public List<CheckSumDupsBasedOn>? CheckSumDupsBasedOnRows { get; set; } = new List<CheckSumDupsBasedOn>();
}
