using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class FixAppointmentRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.DropPrimaryKey(
                name: "PK_Employee",
                table: "Employee");

           

            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "FridgeRegistrations");

            migrationBuilder.DropColumn(
                name: "WarrantyEndDate",
                table: "FridgeRegistrations");

            migrationBuilder.RenameTable(
                name: "Employee",
                newName: "Employees");

            migrationBuilder.RenameTable(
                name: "Appointment",
                newName: "Appointments");

            migrationBuilder.RenameIndex(
                name: "IX_Appointment_FridgeId",
                table: "Appointments",
                newName: "IX_Appointments_FridgeId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointment_EmployeeID",
                table: "Appointments",
                newName: "IX_Appointments_EmployeeID");

            migrationBuilder.RenameIndex(
                name: "IX_Appointment_CustomerID",
                table: "Appointments",
                newName: "IX_Appointments_CustomerID");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubTotal",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PurchaseDate",
                table: "FridgeRegistrations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "FridgeRegistrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FridgeRegistrations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "FridgeRegistrations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "FridgeRegistrations",
                type: "int",
                nullable: true);

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_Employees",
                table: "Employees",
                column: "EmployeeID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Appointments",
                table: "Appointments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FridgeRegistrations_OrderId",
                table: "FridgeRegistrations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerID1",
                table: "Appointments",
                column: "CustomerID1");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EmployeeID1",
                table: "Appointments",
                column: "EmployeeID1");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerID",
                table: "Appointments",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerID1",
                table: "Appointments",
                column: "CustomerID1",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Employees_EmployeeID",
                table: "Appointments",
                column: "EmployeeID",
                principalTable: "Employees",
                principalColumn: "EmployeeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Employees_EmployeeID1",
                table: "Appointments",
                column: "EmployeeID1",
                principalTable: "Employees",
                principalColumn: "EmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_FridgeRegistrations_FridgeId",
                table: "Appointments",
                column: "FridgeId",
                principalTable: "FridgeRegistrations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistrations_Orders_OrderId",
                table: "FridgeRegistrations",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerID",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerID1",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Employees_EmployeeID",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Employees_EmployeeID1",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_FridgeRegistrations_FridgeId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistrations_Orders_OrderId",
                table: "FridgeRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_FridgeRegistrations_OrderId",
                table: "FridgeRegistrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Employees",
                table: "Employees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Appointments",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerID1",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_EmployeeID1",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FridgeRegistrations");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "FridgeRegistrations");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "FridgeRegistrations");

            migrationBuilder.DropColumn(
                name: "CustomerID1",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "EmployeeID1",
                table: "Appointments");

            migrationBuilder.RenameTable(
                name: "Employees",
                newName: "Employee");

            migrationBuilder.RenameTable(
                name: "Appointments",
                newName: "Appointment");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_FridgeId",
                table: "Appointment",
                newName: "IX_Appointment_FridgeId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_EmployeeID",
                table: "Appointment",
                newName: "IX_Appointment_EmployeeID");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_CustomerID",
                table: "Appointment",
                newName: "IX_Appointment_CustomerID");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "Products",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Total",
                table: "Orders",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "SubTotal",
                table: "Orders",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "DeliveryFee",
                table: "Orders",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "OrderItems",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PurchaseDate",
                table: "FridgeRegistrations",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "FridgeRegistrations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstallationDate",
                table: "FridgeRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyEndDate",
                table: "FridgeRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Employee",
                table: "Employee",
                column: "EmployeeID");

           

            
        }
    }
}
