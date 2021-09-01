using System;
using System.Collections.Generic;

#nullable disable

namespace DupesMaint2.Models
{
    public partial class CheckSumDup
    {
        public int Id { get; set; }
        public string Sha { get; set; }
        public string ToDelete { get; set; }
        public string FileExt { get; set; }
    }
}
