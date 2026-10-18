using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Checkout.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_type = table.Column<string>(type: "enum('OneTime','Recurring')", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customer_card",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    customer_token = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    masked_card_no = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_card", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_card_customer",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "transaction",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    customer_card_id = table.Column<int>(type: "int", nullable: true),
                    invoice_no = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    idempotency_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    request_fingerprint = table.Column<string>(type: "char(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "enum('Pending','Succeeded','Failed')", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    flow = table.Column<string>(type: "enum('HostedPage','SavedCard')", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_token = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    redirect_url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    masked_card_no = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_ref = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resp_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resp_desc = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction", x => x.id);
                    table.ForeignKey(
                        name: "fk_transaction_customer",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_customer_card",
                        column: x => x.customer_card_id,
                        principalTable: "customer_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_product",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "customer",
                columns: new[] { "id", "email", "first_name", "last_name" },
                values: new object[,]
                {
                    { 1, "aung.min@example.com", "Aung", "Min" },
                    { 2, "meiling.tan@example.com", "Mei Ling", "Tan" },
                    { 3, "rajesh.kumar@example.com", "Rajesh", "Kumar" }
                });

            migrationBuilder.InsertData(
                table: "product",
                columns: new[] { "id", "currency", "description", "name", "payment_type", "price" },
                values: new object[,]
                {
                    { 1, "SGD", "Single-day gym access.", "Day Pass", "OneTime", 25.00m },
                    { 2, "SGD", "One group fitness class of your choice.", "Class Drop-in", "OneTime", 18.50m },
                    { 3, "SGD", "Unlimited access, billed monthly to a saved card.", "Monthly Membership", "Recurring", 99.00m },
                    { 4, "SGD", "Unlimited access for 12 months, renewed with a saved card.", "Annual Membership", "Recurring", 899.00m }
                });

            migrationBuilder.CreateIndex(
                name: "ux_customer_email",
                table: "customer",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_card_customer_token",
                table: "customer_card",
                columns: new[] { "customer_id", "customer_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transaction_customer_card_id",
                table: "transaction",
                column: "customer_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_customer_id",
                table: "transaction",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_product_id",
                table: "transaction",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_status",
                table: "transaction",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_transaction_idempotency_key",
                table: "transaction",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transaction_invoice_no",
                table: "transaction",
                column: "invoice_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction");

            migrationBuilder.DropTable(
                name: "customer_card");

            migrationBuilder.DropTable(
                name: "product");

            migrationBuilder.DropTable(
                name: "customer");
        }
    }
}
