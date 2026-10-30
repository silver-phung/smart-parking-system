using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRecognitionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_recognition_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RawPlateNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    IsLegit = table.Column<bool>(type: "boolean", nullable: false),
                    VehicleConfidence = table.Column<double>(type: "double precision", nullable: true),
                    VehicleBboxJson = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PlateBboxJson = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InferenceTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_recognition_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recognition_records_CreatedAt",
                table: "ai_recognition_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recognition_records_StaffUserId",
                table: "ai_recognition_records",
                column: "StaffUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_recognition_records");
        }
    }
}
