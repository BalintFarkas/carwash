﻿#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Microsoft.EntityFrameworkCore.Migrations;

namespace CarWash.ClassLibrary.Migrations
{
    public partial class Settings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CalendarIntegration",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NotificationChannel",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarIntegration",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotificationChannel",
                table: "AspNetUsers");
        }
    }
}
