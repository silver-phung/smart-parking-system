using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffDetectionConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RawPlateNumber",
                table: "ai_recognition_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "ai_recognition_records",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNote",
                table: "ai_recognition_records",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "ai_recognition_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ConfirmedByStaffUserId",
                table: "ai_recognition_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedPlateNumber",
                table: "ai_recognition_records",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ai_recognition_records",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationNote",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "ConfirmedByStaffUserId",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "ConfirmedPlateNumber",
                table: "ai_recognition_records");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ai_recognition_records");

            migrationBuilder.AlterColumn<string>(
                name: "RawPlateNumber",
                table: "ai_recognition_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "ai_recognition_records",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
