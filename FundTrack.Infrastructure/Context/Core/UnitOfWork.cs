using System.Collections.Concurrent;
using FundTrack.core.Interfaces;
using FundTrack.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Infrastructure.Context.Core;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        return (IGenericRepository<TEntity>)_repositories.GetOrAdd(
            typeof(TEntity),
            _ => new GenericRepository<TEntity>(_context));
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public Task<List<T>> SqlQueryAsync<T>(string sql, params object[] parameters) where T : class
        => _context.Database.SqlQueryRaw<T>(sql, parameters).ToListAsync();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await operation();
        await transaction.CommitAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}