using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Repositories
{
    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
    {
        private readonly ApplicationContext _context;
        private readonly DbSet<TEntity> _dbSet;
 
        public GenericRepository(ApplicationContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }
 
        public IEnumerable<TEntity> Get()
        {
            return _dbSet.AsNoTracking().ToList();
        }
         
        public IEnumerable<TEntity> Get(Func<TEntity, bool> predicate)
        {
            return _dbSet.AsNoTracking().Where(predicate).ToList();
        }
        public TEntity? FindById(int id)
        {
            return _dbSet.Find(id);
        }
 
        public void Create(TEntity item)
        {
            _dbSet.Add(item);
        }
        public void Update(TEntity item)
        {
            _context.Entry(item).State = EntityState.Modified;
        }
        public void Remove(TEntity item)
        {
            _dbSet.Remove(item);
        }
    }
}
