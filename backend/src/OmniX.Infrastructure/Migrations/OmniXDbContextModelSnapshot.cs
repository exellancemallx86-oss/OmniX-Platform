// ═══════════════════════════════════════════════════════════════════════════
//  OmniX — EF Core Model Snapshot
//  مطلوب بواسطة EF Core لتتبع الـ schema الحالية
// ═══════════════════════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OmniX.Infrastructure.Data;

#nullable disable

namespace OmniX.Infrastructure.Migrations;

[DbContext(typeof(OmniXDbContext))]
partial class OmniXDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal
            .NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
#pragma warning restore 612, 618
    }
}
