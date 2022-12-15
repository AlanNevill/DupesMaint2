using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DupesMaint2.Models;

[Keyless]
public partial class VCheckSumBasedOnGroup
{
    [Required]
    [StringLength(20)]
    [Unicode(false)]
    public string? DupBasedOn { get; set; }

    [Required]
    [StringLength(200)]
    [Unicode(false)]
    public string? BasedOnVal { get; set; }

    public int TheCount { get; set; }
}
