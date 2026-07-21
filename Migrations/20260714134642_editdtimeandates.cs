using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class editdtimeandates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3761));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3769));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3777));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3779));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3781));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3783));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3786));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3788));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3790));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 13, 46, 38, 780, DateTimeKind.Utc).AddTicks(3792));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1285));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1295));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1304));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1307));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1310));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1314));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1318));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1323));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1327));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 22, 33, 54, 98, DateTimeKind.Utc).AddTicks(1330));
        }
    }
}
