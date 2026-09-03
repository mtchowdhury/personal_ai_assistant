using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmdNext.Repository.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class;

        IRepository<TEntity> Repository<TEntity>() where TEntity : class;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        int SaveChanges();

        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
