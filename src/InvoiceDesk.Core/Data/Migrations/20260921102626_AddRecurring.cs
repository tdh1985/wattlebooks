using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurringScheduleId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Every = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OccurrencesCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RecurringScheduleId",
                table: "Invoices",
                column: "RecurringScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_RecurringSchedules_RecurringScheduleId",
                table: "Invoices",
                column: "RecurringScheduleId",
                principalTable: "RecurringSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_RecurringSchedules_RecurringScheduleId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "RecurringSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_RecurringScheduleId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RecurringScheduleId",
                table: "Invoices");
        }
    }
}
