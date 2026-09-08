using System.Collections.Generic;
using System.Threading.Tasks;

namespace GatherUp.Core.Interfaces
{
    public interface IRepository<T> where T : class, IEntity
    {
        Task AddAsync(T entity);
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task UpdateAsync(T entity);
        Task DeleteAsync(int id);
    }
}
