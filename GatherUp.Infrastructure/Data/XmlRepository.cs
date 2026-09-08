using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GatherUp.Core.Interfaces;

namespace GatherUp.Infrastructure.Data
{
    public class XmlRepository<T> : IRepository<T> where T : class, IEntity, new()
    {
        protected readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public XmlRepository(string baseFolder)
        {
            if (!Directory.Exists(baseFolder))
                Directory.CreateDirectory(baseFolder);

            _filePath = Path.Combine(baseFolder, $"{typeof(T).Name}s.xml");
        }

        protected async Task<List<T>> LoadAllAsync()
        {
            if (!File.Exists(_filePath)) return new List<T>();
            var text = await File.ReadAllTextAsync(_filePath);
            return XMLSerializer.DeserializeFromString<List<T>>(text) ?? new List<T>();
        }

        protected async Task SaveAllAsync(List<T> list)
        {
            string tmp = _filePath + ".tmp";
            string xml = XMLSerializer.SerializeToString(list);
            await File.WriteAllTextAsync(tmp, xml);

            if (File.Exists(_filePath))
                File.Delete(_filePath);

            File.Move(tmp, _filePath);
        }

        public virtual async Task AddAsync(T entity)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await LoadAllAsync();
                if (list.Any(x => x.Id == entity.Id))
                    throw new InvalidOperationException($"{typeof(T).Name} with Id={entity.Id} already exists.");
                list.Add(entity);
                await SaveAllAsync(list);
            }
            finally { _lock.Release(); }
        }

        public virtual async Task<T> GetByIdAsync(int id)
        {
            var list = await LoadAllAsync();
            return list.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException($"{typeof(T).Name} עם Id={id} לא נמצא");
        }

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await LoadAllAsync();

        public virtual async Task UpdateAsync(T entity)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await LoadAllAsync();
                int idx = list.FindIndex(x => x.Id == entity.Id);
                if (idx < 0)
                    throw new KeyNotFoundException($"{typeof(T).Name} עם Id={entity.Id} לא נמצא");
                list[idx] = entity;
                await SaveAllAsync(list);
            }
            finally { _lock.Release(); }
        }

        public virtual async Task DeleteAsync(int id)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await LoadAllAsync();
                int removed = list.RemoveAll(x => x.Id == id);
                if (removed == 0)
                    throw new KeyNotFoundException($"{typeof(T).Name} עם Id={id} לא נמצא");
                await SaveAllAsync(list);
            }
            finally { _lock.Release(); }
        }
    }
}
