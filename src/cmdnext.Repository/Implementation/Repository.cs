using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CmdNext.Repository.Contracts;

namespace CmdNext.Repository.Implementation
{
    public class Repository<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : class
    {
        private readonly CmdNextDbContext _dbContext;
        private readonly DbSet<TEntity> _dbSet;

        public Repository(CmdNextDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = dbContext.Set<TEntity>();
        }

        public async Task<TEntity?> GetByIdAsync(TKey id, bool asNoTracking = false, CancellationToken cancellationToken = default)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync(bool asNoTracking = false, CancellationToken cancellationToken = default)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
            return await query.ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool asNoTracking = false,
            CancellationToken cancellationToken = default)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
            return await query.Where(predicate).ToListAsync(cancellationToken);
        }

        public async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool asNoTracking = false,
            CancellationToken cancellationToken = default)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
            return await query.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddRangeAsync(entities, cancellationToken);
        }

        public void Update(TEntity entity)
        {
            _dbSet.Update(entity);
        }

        public void UpdateRange(IEnumerable<TEntity> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public void Delete(TEntity entity)
        {
            _dbSet.Remove(entity);
        }

        public void DeleteRange(IEnumerable<TEntity> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public IQueryable<TEntity> Query()
        {
            return _dbSet;
        }
    }

    public class Repository<TEntity> : Repository<TEntity, int>, IRepository<TEntity> where TEntity : class
    {
        public Repository(CmdNextDbContext dbContext) : base(dbContext)
        {
        }
    }
}
