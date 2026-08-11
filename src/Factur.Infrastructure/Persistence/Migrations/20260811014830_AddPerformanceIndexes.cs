using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Factur.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate_Status",
                table: "Invoices",
                columns: new[] { "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate_Sequence",
                table: "Invoices",
                columns: new[] { "InvoiceDate", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate_Status",
                table: "Invoices",
                columns: new[] { "InvoiceDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_DueDate_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate_Sequence",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate_Status",
                table: "Invoices");
        }
    }
}
