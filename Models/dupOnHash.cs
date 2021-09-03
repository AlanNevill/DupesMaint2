#nullable enable

namespace DupesMaint2.Models
{
#nullable enable

    public partial class dupOnHash
    {
        public int Id { get; set; }
        public string Sha { get; set; }
        public decimal? AverageHash { get; set; }
        public decimal? DifferenceHash { get; set; }
        public decimal? PerceptualHash { get; set; }
    }
}
