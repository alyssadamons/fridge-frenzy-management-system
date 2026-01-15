using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class MakeIdentityUserOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerID1",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Employees_EmployeeID1",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_IdentityUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerID1",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_EmployeeID1",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerID1",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "EmployeeID1",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityUserId",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_IdentityUserId",
                table: "Customers",
                column: "IdentityUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_IdentityUserId",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityUserId",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerID1",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeID1",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerID1",
                table: "Appointments",
                column: "CustomerID1");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EmployeeID1",
                table: "Appointments",
                column: "EmployeeID1");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerID1",
                table: "Appointments",
                column: "CustomerID1",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Employees_EmployeeID1",
                table: "Appointments",
                column: "EmployeeID1",
                principalTable: "Employees",
                principalColumn: "EmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_IdentityUserId",
                table: "Customers",
                column: "IdentityUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
