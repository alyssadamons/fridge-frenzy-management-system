using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations.DashboardDb
{
    /// <inheritdoc />
    public partial class MakeAppointmentColorNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_FridgeRegistrations_FridgeId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistrations_Customers_CustomerId",
                table: "FridgeRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistrations_OrderItem_OrderItemId",
                table: "FridgeRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistrations_Product_ProductId",
                table: "FridgeRegistrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FridgeRegistrations",
                table: "FridgeRegistrations");

            migrationBuilder.RenameTable(
                name: "FridgeRegistrations",
                newName: "FridgeRegistration");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistrations_ProductId",
                table: "FridgeRegistration",
                newName: "IX_FridgeRegistration_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistrations_OrderItemId",
                table: "FridgeRegistration",
                newName: "IX_FridgeRegistration_OrderItemId");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistrations_CustomerId",
                table: "FridgeRegistration",
                newName: "IX_FridgeRegistration_CustomerId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Product",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "OrderItem",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Order",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubTotal",
                table: "Order",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "DeliveryFee",
                table: "Order",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FridgeRegistration",
                table: "FridgeRegistration",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_FridgeRegistration_FridgeId",
                table: "Appointments",
                column: "FridgeId",
                principalTable: "FridgeRegistration",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistration_Customers_CustomerId",
                table: "FridgeRegistration",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistration_OrderItem_OrderItemId",
                table: "FridgeRegistration",
                column: "OrderItemId",
                principalTable: "OrderItem",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistration_Product_ProductId",
                table: "FridgeRegistration",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_FridgeRegistration_FridgeId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistration_Customers_CustomerId",
                table: "FridgeRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistration_OrderItem_OrderItemId",
                table: "FridgeRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_FridgeRegistration_Product_ProductId",
                table: "FridgeRegistration");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FridgeRegistration",
                table: "FridgeRegistration");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Order");

            migrationBuilder.RenameTable(
                name: "FridgeRegistration",
                newName: "FridgeRegistrations");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistration_ProductId",
                table: "FridgeRegistrations",
                newName: "IX_FridgeRegistrations_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistration_OrderItemId",
                table: "FridgeRegistrations",
                newName: "IX_FridgeRegistrations_OrderItemId");

            migrationBuilder.RenameIndex(
                name: "IX_FridgeRegistration_CustomerId",
                table: "FridgeRegistrations",
                newName: "IX_FridgeRegistrations_CustomerId");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "Product",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "OrderItem",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Total",
                table: "Order",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "SubTotal",
                table: "Order",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "DeliveryFee",
                table: "Order",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FridgeRegistrations",
                table: "FridgeRegistrations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_FridgeRegistrations_FridgeId",
                table: "Appointments",
                column: "FridgeId",
                principalTable: "FridgeRegistrations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistrations_Customers_CustomerId",
                table: "FridgeRegistrations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistrations_OrderItem_OrderItemId",
                table: "FridgeRegistrations",
                column: "OrderItemId",
                principalTable: "OrderItem",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FridgeRegistrations_Product_ProductId",
                table: "FridgeRegistrations",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "ProductId");
        }
    }
}
