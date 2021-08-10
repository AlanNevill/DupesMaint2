using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2
{

    [Table("CheckSumHasDuplicates")]
    public partial class CheckSumHasDuplicates
    {

        [StringLength(200)]
        public string SHA { get; set; }

        public int DupeCount { get; set; }

     }
}
