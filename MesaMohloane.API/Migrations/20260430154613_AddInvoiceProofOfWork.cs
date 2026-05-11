using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaMohloane.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceProofOfWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProofOfWorkImageUrls",
                table: "Invoices",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofOfWorkImageUrls",
                table: "Invoices");
        }
    }
}
