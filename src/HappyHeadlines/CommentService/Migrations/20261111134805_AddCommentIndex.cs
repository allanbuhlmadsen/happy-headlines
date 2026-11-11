using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommentService.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Comments_ArticleContinent_ArticleId_PostedAt",
                table: "Comments",
                columns: new[] { "ArticleContinent", "ArticleId", "PostedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_ArticleContinent_ArticleId_PostedAt",
                table: "Comments");
        }
    }
}
