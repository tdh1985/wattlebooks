using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "Abn", table: "Profiles", newName: "TaxNumber");
            migrationBuilder.RenameColumn(name: "Bsb", table: "Profiles", newName: "BankCode");
            migrationBuilder.RenameColumn(name: "GstRegistered", table: "Profiles", newName: "TaxRegistered");
            migrationBuilder.RenameColumn(name: "GstRateBasisPoints", table: "Profiles", newName: "TaxRatePpm");
            migrationBuilder.RenameColumn(name: "Abn", table: "Clients", newName: "TaxNumber");
            migrationBuilder.RenameColumn(name: "GstEnabled", table: "Invoices", newName: "TaxEnabled");
            migrationBuilder.RenameColumn(name: "GstRateBasisPoints", table: "Invoices", newName: "TaxRatePpm");
            migrationBuilder.RenameColumn(name: "GstCents", table: "Transactions", newName: "TaxCents");

            migrationBuilder.AddColumn<string>(name: "Country", table: "Profiles", type: "TEXT", nullable: false, defaultValue: "AU");
            migrationBuilder.AddColumn<string>(name: "Region", table: "Profiles", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>(name: "ReducedRatePpm", table: "Profiles", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TaxPeriodMonths", table: "Profiles", type: "INTEGER", nullable: false, defaultValue: 3);
            migrationBuilder.AddColumn<int>(name: "TaxPeriodEndMonth", table: "Profiles", type: "INTEGER", nullable: false, defaultValue: 3);
            migrationBuilder.AddColumn<string>(name: "SwiftCode", table: "Profiles", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "OverseasNote", table: "Profiles", type: "TEXT", nullable: false,
                defaultValue: "No GST has been charged, as this is a supply to a client outside Australia.");
            migrationBuilder.AddColumn<string>(name: "Country", table: "Clients", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Region", table: "Clients", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Currency", table: "Clients", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>(name: "ReducedRatePpm", table: "Invoices", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "Currency", table: "Invoices", type: "TEXT", nullable: false, defaultValue: "AUD");
            migrationBuilder.AddColumn<int>(name: "TaxCode", table: "InvoiceLines", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<long>(name: "ForeignAmountCents", table: "Transactions", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ForeignCurrency", table: "Transactions", type: "TEXT", nullable: false, defaultValue: "");

            // 1.1.0 kept rates in basis points and gst-free as a flag
            migrationBuilder.Sql("UPDATE Profiles SET TaxRatePpm = TaxRatePpm * 100;");
            migrationBuilder.Sql("UPDATE Invoices SET TaxRatePpm = TaxRatePpm * 100;");
            migrationBuilder.Sql("UPDATE InvoiceLines SET TaxCode = 1 WHERE GstFree = 1;");
            migrationBuilder.DropColumn(name: "GstFree", table: "InvoiceLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "GstFree", table: "InvoiceLines", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.Sql("UPDATE InvoiceLines SET GstFree = 1 WHERE TaxCode <> 0;");
            migrationBuilder.Sql("UPDATE Invoices SET TaxRatePpm = TaxRatePpm / 100;");
            migrationBuilder.Sql("UPDATE Profiles SET TaxRatePpm = TaxRatePpm / 100;");

            migrationBuilder.DropColumn(name: "ForeignCurrency", table: "Transactions");
            migrationBuilder.DropColumn(name: "ForeignAmountCents", table: "Transactions");
            migrationBuilder.DropColumn(name: "TaxCode", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "Currency", table: "Invoices");
            migrationBuilder.DropColumn(name: "ReducedRatePpm", table: "Invoices");
            migrationBuilder.DropColumn(name: "Currency", table: "Clients");
            migrationBuilder.DropColumn(name: "Region", table: "Clients");
            migrationBuilder.DropColumn(name: "Country", table: "Clients");
            migrationBuilder.DropColumn(name: "OverseasNote", table: "Profiles");
            migrationBuilder.DropColumn(name: "SwiftCode", table: "Profiles");
            migrationBuilder.DropColumn(name: "TaxPeriodEndMonth", table: "Profiles");
            migrationBuilder.DropColumn(name: "TaxPeriodMonths", table: "Profiles");
            migrationBuilder.DropColumn(name: "ReducedRatePpm", table: "Profiles");
            migrationBuilder.DropColumn(name: "Region", table: "Profiles");
            migrationBuilder.DropColumn(name: "Country", table: "Profiles");

            migrationBuilder.RenameColumn(name: "TaxCents", table: "Transactions", newName: "GstCents");
            migrationBuilder.RenameColumn(name: "TaxRatePpm", table: "Invoices", newName: "GstRateBasisPoints");
            migrationBuilder.RenameColumn(name: "TaxEnabled", table: "Invoices", newName: "GstEnabled");
            migrationBuilder.RenameColumn(name: "TaxNumber", table: "Clients", newName: "Abn");
            migrationBuilder.RenameColumn(name: "TaxRatePpm", table: "Profiles", newName: "GstRateBasisPoints");
            migrationBuilder.RenameColumn(name: "TaxRegistered", table: "Profiles", newName: "GstRegistered");
            migrationBuilder.RenameColumn(name: "BankCode", table: "Profiles", newName: "Bsb");
            migrationBuilder.RenameColumn(name: "TaxNumber", table: "Profiles", newName: "Abn");
        }
    }
}
