using System.ComponentModel.DataAnnotations.Schema;

namespace DupesMaint2.Models
{
#nullable disable

    public partial class CheckSumDupsBasedOn
    {
        public int CheckSumId { get; set; }
        public string DupBasedOn { get; set; }
        public string BasedOnVal { get; set; }

        // navigation properties
        //public int CheckSumDupsCheckSumId { get; set; }

        public CheckSumDups CheckSumDups { get; set; }
    }
}
