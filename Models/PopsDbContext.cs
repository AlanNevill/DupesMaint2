using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

#nullable disable

namespace DupesMaint2.Models
{
    public partial class PopsDbContext : DbContext
    {
        //static LoggerFactory object
        public static readonly ILoggerFactory MyLoggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

        public PopsDbContext()
        {
        }

        public PopsDbContext(DbContextOptions<PopsDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CheckSum> CheckSums { get; set; }
        public virtual DbSet<CheckSumDups> CheckSumDups { get; set; }

        public virtual DbSet<dupOnHash> dupOnHashes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // TODO: used to enable console logging of SQL commands issued by DBcontext
/*            optionsBuilder.UseLoggerFactory(MyLoggerFactory)  //tie-up DbContext with LoggerFactory object
                .EnableSensitiveDataLogging()
                .UseSqlServer(@"Server=SNOWBALL\MSSQLSERVER01;Database=Pops;Trusted_Connection=True;");
*/
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

                entity.Property(e => e.Notes2)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.AverageHash).HasColumnType("decimal(20, 0)");
                entity.Property(e => e.DifferenceHash).HasColumnType("decimal(20, 0)");
                entity.Property(e => e.PerceptualHash).HasColumnType("decimal(20, 0)");

                entity.Property(e => e.SCreateDateTime)
                    .HasMaxLength(30)
                    .IsUnicode(false);

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

            modelBuilder.Entity<CheckSumDups>(entity =>
            {
                entity.ToTable("CheckSumDups");

                entity.Property(e => e.Id);

                entity.HasKey(e => e.Id);

                entity.Property(e => e.CheckSumId);

                entity.Property(e => e.DupBasedOn)
                    .HasMaxLength(20);

                entity.Property(e => e.Sha)
                    .HasMaxLength(200)
                    .IsUnicode(false)
                    .HasColumnName("SHA");


                entity.Property(e => e.AverageHash).HasColumnType("decimal(20, 0)");
                entity.Property(e => e.DifferenceHash).HasColumnType("decimal(20, 0)");
                entity.Property(e => e.PerceptualHash).HasColumnType("decimal(20, 0)");

                entity.Property(e => e.ToDelete)
                    .IsRequired()
                    .HasMaxLength(1)
                    .IsUnicode(false)
                    .HasDefaultValueSql("('N')")
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<dupOnHash>().HasNoKey();

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
