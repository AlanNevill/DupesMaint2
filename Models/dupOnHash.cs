#nullable enable

namespace DupesMaint2.Models
{
#nullable disable

    public partial class DupOnHash
    {
        public int CheckSumId { get; set; }
        public string DupBasedOn { get; set; }
        public string BasedOnVal { get; set; }
    }
}
