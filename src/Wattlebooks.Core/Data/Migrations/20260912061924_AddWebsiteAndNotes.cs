using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wattlebooks.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultInvoiceNotes",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrivateNotes",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNote",
                table: "Clients",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Clients",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DefaultInvoiceNotes",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "PrivateNotes",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "InvoiceNote",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Clients");
        }
    }
}
