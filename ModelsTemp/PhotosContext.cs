using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DupesMaint2.ModelsTemp;

public partial class PhotosContext : DbContext
{
    public PhotosContext()
    {
    }

    public PhotosContext(DbContextOptions<PhotosContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CheckSum> CheckSums { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("data source=.;initial catalog=Photos;integrated security=True;TrustServerCertificate=yes;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<CheckSum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ID");

            entity.Property(e => e.CreateMonth).HasComputedColumnSql("(datepart(month,[CreateDateTime]))", false);
            entity.Property(e => e.CreateYear).HasComputedColumnSql("(datepart(year,[CreateDateTime]))", false);
            entity.Property(e => e.FileFullName).HasComputedColumnSql("((([Folder]+'\\')+[TheFileName])+[FileExt])", false);
            entity.Property(e => e.FormatValid).IsFixedLength();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
