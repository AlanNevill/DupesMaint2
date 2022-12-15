using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using DupesMaint2.Models;

namespace DupesMaint2.Models
{
    public partial class PhotosDbContext : DbContext
    {
        public PhotosDbContext()
        {
        }

        public PhotosDbContext(DbContextOptions<PhotosDbContext> options)
            : base(options)
        {}

        public virtual DbSet<CheckSum> CheckSum { get; set; }

        public virtual DbSet<CheckSumDupsBasedOn> CheckSumDupsBasedOn { get; set; }

        public virtual DbSet<VCheckSumBasedOnGroup> VCheckSumBasedOnGroup { get; set; }

        public virtual DbSet<HashTypes> HashValueTypes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
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
                entity.Property(e => e.ToDelete)
                               .HasDefaultValueSql("('N')")
                               .IsFixedLength();
            });

            // implement foreign kep automatic collection CheckSum owns 0,1 or many ChackSumDupsBasedOn
            modelBuilder.Entity<CheckSumDupsBasedOn>(entity =>
            {
                entity.HasOne(d => d.CheckSum)
                    .WithMany(p => p.CheckSumDupsBasedOn)
                    //.HasForeignKey(c => c.CheckSumId)
                    //.HasPrincipalKey(c => c.Id)
                    .HasConstraintName("FK_CheckSumDupsBasedOn_CheckSumId");
            });

            modelBuilder.Entity<HashTypes>(entity => entity.HasNoKey());

            modelBuilder.Entity<VCheckSumBasedOnGroup>(entity =>
            {
                entity.ToView("vCheckSumBasedOn_Group");
            });


            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
