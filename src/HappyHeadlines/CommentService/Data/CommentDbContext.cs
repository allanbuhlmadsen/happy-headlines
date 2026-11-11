using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Data;

public class CommentDbContext : DbContext
{
    public CommentDbContext(DbContextOptions<CommentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every lookup of an article's comments filters on the first two
        // columns and sorts by the third, so the index covers the whole query.
        modelBuilder.Entity<Comment>()
            .HasIndex(c => new { c.ArticleContinent, c.ArticleId, c.PostedAt });
    }
}