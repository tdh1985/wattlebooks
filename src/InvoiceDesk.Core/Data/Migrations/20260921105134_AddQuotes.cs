using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextQuoteNumber",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "QuotePrefix",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "QUO-");

            migrationBuilder.AddColumn<int>(
                name: "QuoteValidDays",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnsweredAt",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConvertedFromId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ConvertedFromId",
                table: "Invoices",
                column: "ConvertedFromId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_ConvertedFromId",
                table: "Invoices",
                column: "ConvertedFromId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_ConvertedFromId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ConvertedFromId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "NextQuoteNumber",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "QuotePrefix",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "QuoteValidDays",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "AnsweredAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ConvertedFromId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Invoices");
        }
    }
}
