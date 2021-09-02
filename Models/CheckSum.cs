using System;
using System.Collections.Generic;

#nullable enable

namespace DupesMaint2.Models
{
    public partial class CheckSum
    {
        public int Id { get; set; }
        public string Sha { get; set; }
        public string Folder { get; set; }
        public string TheFileName { get; set; }
        public string FileExt { get; set; }
        public int FileSize { get; set; }
        public DateTime FileCreateDt { get; set; }
        public int TimerMs { get; set; }
        public TimeSpan? Mp4duration { get; set; }
        public string? Notes { get; set; }
        public string? Notes2 { get; set; }
        public string? MediaFileType { get; set; }
        public DateTime? CreateDateTime { get; set; }
        public string? SCreateDateTime { get; set; }
        public decimal? AverageHash { get; set; }
        public decimal? DifferenceHash { get; set; }
        public decimal? PerceptualHash { get; set; }
        public string FileFullName { get; set; }
    }
}
