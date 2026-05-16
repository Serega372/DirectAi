using AuthService.Core.Entities;
using AuthService.Infrastructure.EntityConfigurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.EntityConfigurations;

public sealed class RefreshTokenConfiguration : BaseEntityConfiguration<RefreshTokenEntity>
{
    public override void Configure(EntityTypeBuilder<RefreshTokenEntity> entity)
    {
        base.Configure(entity);

        entity.ToTable("refresh_tokens_tbl");

        entity.Property(entity => entity.Token)
            .IsRequired();

        entity.Property(entity => entity.RevocationDate)
            .IsRequired();

        entity.Property(entity => entity.ExpirationDate)
            .IsRequired();

        entity.Property(entity => entity.UserId)
            .IsRequired();
    }
}