using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaMohloane.API.Migrations
{
    /// <inheritdoc />
    public partial class FixExistingUserStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET RegistrationStatus = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
