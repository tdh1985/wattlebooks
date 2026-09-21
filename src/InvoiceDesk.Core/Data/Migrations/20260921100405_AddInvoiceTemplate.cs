using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceTemplate",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "classic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceTemplate",
                table: "Profiles");
        }
    }
}
