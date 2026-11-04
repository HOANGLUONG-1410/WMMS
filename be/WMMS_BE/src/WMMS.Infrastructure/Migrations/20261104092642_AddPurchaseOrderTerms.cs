using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "terms",
                table: "purchase_orders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "terms",
                table: "purchase_orders");
        }
    }
}
