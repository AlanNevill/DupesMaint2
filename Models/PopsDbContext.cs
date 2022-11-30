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
        // TODO: static LoggerFactory object for logging SQL commands to the console
        public static readonly ILoggerFactory MyLoggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

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
                entity.ToTable("CheckSumDups");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .IsRequired();

                entity.Property(e => e.CheckSumId)
                    .IsRequired();

                entity.Property(e => e.ToDelete)
                    .IsRequired()
                    .HasMaxLength(1)
                    .IsUnicode(false)
                    .HasDefaultValueSql("('N')")
                    .IsFixedLength(true);
            });

            modelBuilder.Entity<CheckSumDupsBasedOn>(entity =>
            {
                entity.ToTable("CheckSumDupsBasedOn");

                entity.HasKey(e => new { e.CheckSumId, e.DupBasedOn});

                entity.Property(e => e.DupBasedOn)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.BasedOnVal)
                    .HasMaxLength(200)
                    .IsUnicode(false);
            });


            modelBuilder.Entity<DupOnHash>().HasNoKey();

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
