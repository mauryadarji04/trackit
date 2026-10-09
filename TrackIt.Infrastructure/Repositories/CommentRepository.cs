using Dapper;
using TrackIt.Core.Dtos;
using TrackIt.Core.Entities;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Infrastructure.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly ISqlConnectionFactory _factory;
    private readonly AppDbContext _db;

    public CommentRepository(ISqlConnectionFactory factory, AppDbContext db)
    {
        _factory = factory;
        _db = db;
    }

    public async Task<IReadOnlyList<CommentDto>> GetByTicketAsync(int ticketId)
    {
        const string sql = @"
            SELECT c.CommentId, c.TicketId, c.UserId, u.FullName AS UserName,
                   c.ParentCommentId, c.CommentText, c.CreatedAt
            FROM dbo.TicketComments c
            JOIN dbo.AspNetUsers u ON u.Id = c.UserId
            WHERE c.TicketId = @TicketId
            ORDER BY c.CreatedAt, c.CommentId;";

        await using var conn = _factory.Create();
        return (await conn.QueryAsync<CommentDto>(sql, new { TicketId = ticketId })).ToList();
    }

    public async Task<CommentDto?> GetByIdAsync(int commentId)
    {
        const string sql = @"
            SELECT c.CommentId, c.TicketId, c.UserId, u.FullName AS UserName,
                   c.ParentCommentId, c.CommentText, c.CreatedAt
            FROM dbo.TicketComments c
            JOIN dbo.AspNetUsers u ON u.Id = c.UserId
            WHERE c.CommentId = @CommentId;";

        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<CommentDto>(sql, new { CommentId = commentId });
    }

    public async Task<int> AddAsync(TicketComment comment)
    {
        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync();
        return comment.CommentId;
    }
}
