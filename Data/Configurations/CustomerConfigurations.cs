using KorridorX.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.CountryCode);
        builder.HasIndex(x => x.KycStatus);

        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.MiddleName).HasMaxLength(100);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.StateOrProvince).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.AddressLine1).HasMaxLength(250);
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.PostalCode).HasMaxLength(50);
        builder.Property(x => x.BlaaizCustomerId).HasMaxLength(150);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasIndex(x => x.CountryCode);
        builder.HasIndex(x => x.KybStatus);

        builder.Property(x => x.BusinessName).HasMaxLength(200);
        builder.Property(x => x.RegistrationNumber).HasMaxLength(100);
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.StateOrProvince).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.AddressLine1).HasMaxLength(250);
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.PostalCode).HasMaxLength(50);
        builder.Property(x => x.ContactEmail).HasMaxLength(255);
        builder.Property(x => x.ContactPhone).HasMaxLength(50);
        builder.Property(x => x.BlaaizBusinessCustomerId).HasMaxLength(150);

        builder.HasOne(x => x.OwnerUser)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessUserConfiguration : IEntityTypeConfiguration<BusinessUser>
{
    public void Configure(EntityTypeBuilder<BusinessUser> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.UserId }).IsUnique();

        builder.HasOne(x => x.BusinessProfile)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}