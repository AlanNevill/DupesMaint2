using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace DupesMaint2.Models
{
    public partial class PopsDbContext : DbContext
    {
        public PopsDbContext()
        {
        }

        public PopsDbContext(DbContextOptions<PopsDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CheckSum> CheckSums { get; set; }
        public virtual DbSet<CheckSumDup> CheckSumDups { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(HelperLib.ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "SQL_Latin1_General_CP1_CI_AS");

            modelBuilder.Entity<CheckSum>(entity =>
            {
                entity.ToTable("CheckSum");

                entity.Property(e => e.AverageHash).HasColumnType("decimal(20, 0)");

                entity.Property(e => e.DifferenceHash).HasColumnType("decimal(20, 0)");

                entity.Property(e => e.FileCreateDt)
                    .HasColumnType("smalldatetime")
                    .HasDefaultValueSql("('1900-01-01')");

                entity.Property(e => e.FileExt)
                    .IsRequired()
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.FileFullName)
                    .IsRequired()
                    .HasMaxLength(401)
                    .IsUnicode(false)
                    .HasComputedColumnSql("(([Folder]+'\\')+[TheFileName])", false);

                entity.Property(e => e.Folder)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.MediaFileType)
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.Mp4duration).HasColumnName("MP4Duration");

                entity.Property(e => e.Notes)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.PerceptualHash).HasColumnType("decimal(20, 0)");

                entity.Property(e => e.ScreateDateTime)
                    .HasMaxLength(30)
                    .IsUnicode(false)
                    .HasColumnName("SCreateDateTime");

                entity.Property(e => e.Sha)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false)
                    .HasColumnName("SHA");

                entity.Property(e => e.TheFileName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<CheckSumDup>(entity =>
            {
                entity.HasKey(e => new { e.Id, e.Sha });

                entity.Property(e => e.Sha)
                    .HasMaxLength(200)
                    .IsUnicode(false)
                    .HasColumnName("SHA");

                entity.Property(e => e.FileExt)
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.ToDelete)
                    .IsRequired()
                    .HasMaxLength(1)
                    .IsUnicode(false)
                    .HasDefaultValueSql("('N')")
                    .IsFixedLength(true);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
