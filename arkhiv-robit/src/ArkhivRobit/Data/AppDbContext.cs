using ArkhivRobit.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArkhivRobit.Data;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Work> Works => Set<Work>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    /// <summary>Ключі шифрування cookie, щоб вхід не злітав після перезапуску сервера.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Work>(e =>
        {
            e.Property(w => w.Title).HasMaxLength(300).IsRequired();
            e.Property(w => w.StudentName).HasMaxLength(200).IsRequired();
            e.Property(w => w.GroupName).HasMaxLength(50).IsRequired();
            e.Property(w => w.Supervisor).HasMaxLength(200);
            e.Property(w => w.Description).HasMaxLength(4000);
            e.Property(w => w.FileKey).HasMaxLength(300).IsRequired();
            e.Property(w => w.FileName).HasMaxLength(300).IsRequired();
            e.Property(w => w.ContentType).HasMaxLength(150);
            e.Property(w => w.SearchText).HasMaxLength(1000);
            e.Property(w => w.UploadedByName).HasMaxLength(200);

            e.HasIndex(w => w.FileKey).IsUnique();
            e.HasIndex(w => new { w.Year, w.GroupName });
            e.HasIndex(w => w.GroupName);

            e.HasOne(w => w.UploadedByTeacher)
                .WithMany(t => t.Works)
                .HasForeignKey(w => w.UploadedByTeacherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Teacher>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.CodeHash).HasMaxLength(64).IsRequired();
            e.HasIndex(t => t.CodeHash).IsUnique();
        });
    }
}
