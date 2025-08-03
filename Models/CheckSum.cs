using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;

public partial class CheckSum
{
    [Key]
    public int Id { get; set; }

    [Column( "SHA" )]
    [StringLength( 200 )]
    [Unicode( false )]
    public string? Sha { get; set; }

    [Required]
    [StringLength( 200 )]
    [Unicode( false )]
    public string Folder { get; set; } = null!;

    [Required]
    [StringLength( 200 )]
    [Unicode( false )]
    public string TheFileName { get; set; } = null!;

    [Required]
    [StringLength( 10 )]
    [Unicode( false )]
    public string FileExt { get; set; } = null!;

    public int? FileSize { get; set; }

    [StringLength( 1000 )]
    [Unicode( false )]
    public string? Notes { get; set; }

    [StringLength( 1000 )]
    [Unicode( false )]
    public string? Notes2 { get; set; }

    [StringLength( 10 )]
    [Unicode( false )]
    public string? MediaFileType { get; set; }

    public DateTime? CreateDateTime { get; set; }

    [Column( TypeName = "decimal(20, 0)" )]
    public decimal? AverageHash { get; set; }

    [Column( TypeName = "decimal(20, 0)" )]
    public decimal? DifferenceHash { get; set; }

    [Column( TypeName = "decimal(20, 0)" )]
    public decimal? PerceptualHash { get; set; }

    [StringLength( 1 )]
    [Unicode( false )]
    public string? FormatValid { get; set; }

    public string FileFullName => Path.Combine( Folder!, TheFileName! );

    public int? CreateYear { get; set; }

    public int? CreateMonth { get; set; }


    [InverseProperty( "CheckSum" )]
    public virtual ICollection<CheckSumDupsBasedOn> CheckSumDupsBasedOn { get; } = new List<CheckSumDupsBasedOn>();

    [NotMapped]
    public string? ImageFileName { get; set; }
}
