using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

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

        public virtual DbSet<CheckSum> CheckSum { get; set; }

        public virtual DbSet<CheckSumDups> CheckSumDups { get; set; }

        public virtual DbSet<DupOnHash> DupOnHash { get; set; }

        public virtual DbSet<CheckSumDupsBasedOn> CheckSumDupsBasedOn { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // TODO: used to enable console logging of SQL commands issued by DBcontext
            //optionsBuilder.UseLoggerFactory(MyLoggerFactory)  //tie-up DbContext with LoggerFactory object
            //    .EnableSensitiveDataLogging()
            //    .UseSqlServer(@"Server=SNOWBALL\MSSQLSERVER01;Database=Pops;Trusted_Connection=True;");

            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(Program._cnStr);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "SQL_Latin1_General_CP1_CI_AS");

            modelBuilder.Entity<CheckSum>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_ID");

                entity.Property(e => e.CreateMonth).HasComputedColumnSql("(datepart(month,[CreateDateTime]))", false);
                entity.Property(e => e.CreateYear).HasComputedColumnSql("(datepart(year,[CreateDateTime]))", false);
                entity.Property(e => e.FileFullName).HasComputedColumnSql("(([Folder]+'\\')+[TheFileName])", false);
                entity.Property(e => e.FormatValid).IsFixedLength();
            });

            modelBuilder.Entity<CheckSumDups>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_CheckSumDups_ID");

                entity.Property(e => e.ToDelete)
                    .HasDefaultValueSql("('N')")
                    .IsFixedLength();

                entity.HasOne(d => d.Checksum).WithOne(p => p.CheckSumDups).HasConstraintName("fk_CheckSumDups_CheckSum");
            });

            modelBuilder.Entity<CheckSumDupsBasedOn>(entity =>
            {
                entity.HasOne(d => d.CheckSum).WithMany(p => p.CheckSumDupsBasedOn)
                    .HasPrincipalKey(p => p.ChecksumId);
            });


            modelBuilder.Entity<DupOnHash>().HasNoKey();

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
