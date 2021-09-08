using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable

namespace DupesMaint2.Models
{
    public partial class CheckSumDups
    {
        [Key]
        public int Id { get; set; }
        public int CheckSumId { get; set; }
        public string? DupBasedOn { get; set; }
        public string? Sha { get; set; }
        public decimal? AverageHash { get; set; }
        public decimal? DifferenceHash { get; set; }
        public decimal? PerceptualHash { get; set; }
        public string ToDelete { get; set; } = "N";

        // Navigation property
        public List<CheckSumDupsBasedOn>? CheckSumDupsBasedOnRows { get; set; } = new List<CheckSumDupsBasedOn>();
    }
}
