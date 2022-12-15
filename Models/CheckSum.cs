using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;

public partial class CheckSum
{
    [Key]
    public int Id { get; set; }

    [Column("SHA")]
    [StringLength(200)]
    [Unicode(false)]
    public string? Sha { get; set; }

    [StringLength(200)]
    [Unicode(false)]
    public string Folder { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string TheFileName { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string FileExt { get; set; } = null!;

    public int? FileSize { get; set; }

    [Column(TypeName = "smalldatetime")]
    public DateTime? FileCreateDt { get; set; }

    public int? TimerMs { get; set; }

    [Column("MP4Duration")]
    public TimeSpan? Mp4duration { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? Notes { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? Notes2 { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? MediaFileType { get; set; }

    public DateTime? CreateDateTime { get; set; }

    [Column(TypeName = "decimal(20, 0)")]
    public decimal? AverageHash { get; set; }

    [Column(TypeName = "decimal(20, 0)")]
    public decimal? DifferenceHash { get; set; }

    [Column(TypeName = "decimal(20, 0)")]
    public decimal? PerceptualHash { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string? FormatValid { get; set; }

    [StringLength(411)]
    [Unicode(false)]
    public string FileFullName { get; set; } = null!;

    public int? CreateYear { get; set; }

    public int? CreateMonth { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string? ToDelete { get; set; }

    [InverseProperty("CheckSum")]
    public ICollection<CheckSumDupsBasedOn> CheckSumDupsBasedOn { get; set; } //= new List<CheckSumDupsBasedOn>();
}
