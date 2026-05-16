using AuthService.Core.Entities;
using AuthService.Infrastructure.EntityConfigurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.EntityConfigurations;

public sealed class AccessTokenConfiguration : BaseEntityConfiguration<AccessTokenEntity>
{
    public override void Configure(EntityTypeBuilder<AccessTokenEntity> entity)
    {
        base.Configure(entity);

        entity.ToTable("access_tokens_tbl");

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