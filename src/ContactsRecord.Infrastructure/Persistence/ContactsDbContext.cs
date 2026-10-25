using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;
using ContactsRecord.Domain.Imports;
using ContactsRecord.Infrastructure.Persistence.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContactsRecord.Infrastructure.Persistence;

/// <summary>
/// Defines the database schema and runs migrations only. All data access goes through Dapper.
/// </summary>
public sealed class ContactsDbContext(DbContextOptions<ContactsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContactPersistenceDto>(contact =>
        {
            contact.ToTable("contacts");
            contact.HasKey(c => c.Id);
            contact.Property(c => c.Id).ValueGeneratedNever();
            contact.Property(c => c.Name).HasMaxLength(Contact.NameMaxLength);
            contact.Property(c => c.Email).HasMaxLength(Email.MaxLength);
            contact.Property(c => c.Phone).HasMaxLength(PhoneNumber.MaxLength);

            // Full (non-partial) index: inactive contacts keep their phone reserved.
            contact.HasIndex(c => c.Phone).IsUnique();
        });

        modelBuilder.Entity<UserPersistenceDto>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Email).HasMaxLength(Email.MaxLength);
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ContactImportPersistenceDto>(import =>
        {
            import.ToTable("contact_imports");
            import.HasKey(i => i.Id);
            import.Property(i => i.Id).ValueGeneratedNever();
            import.Property(i => i.FileName).HasMaxLength(ContactImport.FileNameMaxLength);
            import.Property(i => i.Status).HasMaxLength(StatusMaxLength);
            // Import history is listed most recent first.
            import.HasIndex(i => i.CreatedAt);
        });

        modelBuilder.Entity<ImportRowErrorPersistenceDto>(error =>
        {
            error.ToTable("contact_import_errors");
            error.HasKey(e => e.Id);
            error.Property(e => e.Id).UseIdentityAlwaysColumn();
            error.Property(e => e.Kind).HasMaxLength(StatusMaxLength);
            error.HasOne<ContactImportPersistenceDto>().WithMany()
                .HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Cascade);
            // Errors are listed per import, in file order.
            error.HasIndex(e => new { e.ImportId, e.LineNumber });
        });
    }

    private const int StatusMaxLength = 30;
}
