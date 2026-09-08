using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Interfaces;

namespace GatherUp.Infrastructure.Data
{
    public class MemoryRepository<T> : IRepository<T> where T : class, IEntity
    {
        private readonly List<T> _data = new();
        private readonly object _lockObject = new();

        public Task AddAsync(T entity)
        {
            lock (_lockObject)
            {
                _data.Add(entity);
            }
            return Task.CompletedTask;
        }

        public Task<T> GetByIdAsync(int id)
        {
            lock (_lockObject)
            {
                var item = _data.FirstOrDefault(x => x.Id == id)
                    ?? throw new KeyNotFoundException($"{typeof(T).Name} עם Id={id} לא נמצא");
                return Task.FromResult(item);
            }
        }

        public Task<IEnumerable<T>> GetAllAsync()
        {
            lock (_lockObject)
            {
                return Task.FromResult<IEnumerable<T>>(_data.ToList());
            }
        }

        public Task UpdateAsync(T entity)
        {
            lock (_lockObject)
            {
                var existing = _data.FirstOrDefault(x => x.Id == entity.Id)
                    ?? throw new KeyNotFoundException($"{typeof(T).Name} עם Id={entity.Id} לא נמצא");
                _data.Remove(existing);
                _data.Add(entity);
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id)
        {
            lock (_lockObject)
            {
                var existing = _data.FirstOrDefault(x => x.Id == id)
                    ?? throw new KeyNotFoundException($"{typeof(T).Name} עם Id={id} לא נמצא");
                _data.Remove(existing);
            }
            return Task.CompletedTask;
        }
    }
}
