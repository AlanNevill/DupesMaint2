using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models;

[Table("FileExtensionTypes")]
public partial class FileExtensionTypes
{
    [Required]
    public string? Type { get; set; }

    [Required]
    public string? Group { get; set; }

}
