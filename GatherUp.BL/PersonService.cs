using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Exceptions;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public class PersonService
    {
        private readonly IRepository<Participant>  _participantRepo;
        private readonly IRepository<EventManager> _managerRepo;
        private readonly IRepository<EventHost>    _hostRepo;

        public PersonService(
            IRepository<Participant>  participantRepo,
            IRepository<EventManager> managerRepo,
            IRepository<EventHost>    hostRepo)
        {
            _participantRepo = participantRepo;
            _managerRepo     = managerRepo;
            _hostRepo        = hostRepo;
        }

        public async Task<Participant> RegisterUserAsync(string name, string email, string password = "")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidInputException(nameof(name), "name cannot be empty");
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidInputException(nameof(email), "email cannot be empty");

            var allParticipants = (await _participantRepo.GetAllAsync()).ToList();
            var allManagers     = (await _managerRepo.GetAllAsync()).ToList();

            if (allParticipants.Any(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) ||
                allManagers.Any(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidInputException(nameof(email), "email already registered");

            int maxParticipant = allParticipants.Any() ? allParticipants.Max(p => p.Id) : 0;
            int maxManager     = allManagers.Any()     ? allManagers.Max(m => m.Id)     : 0;
            int newId          = Math.Max(maxParticipant, maxManager) + 1;

            var participant = new Participant { Id = newId, Name = name, Email = email, Password = password };
            var manager     = new EventManager { Id = newId, Name = name, Email = email, Password = password };

            await _participantRepo.AddAsync(participant);
            await _managerRepo.AddAsync(manager);
            return participant;
        }

        public async Task<Participant> RegisterParticipantAsync(string name, string email, string password = "")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidInputException(nameof(name), "name cannot be empty");
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidInputException(nameof(email), "email cannot be empty");

            var all = (await _participantRepo.GetAllAsync()).ToList();

            if (all.Any(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidInputException(nameof(email), "email already registered");

            int newId = all.Any() ? all.Max(p => p.Id) + 1 : 1;
            var participant = new Participant { Id = newId, Name = name, Email = email, Password = password };
            await _participantRepo.AddAsync(participant);
            return participant;
        }

        public async Task<Participant?> GetParticipantByEmailAndPasswordAsync(string email, string password) =>
            (await _participantRepo.GetAllAsync())
                .FirstOrDefault(p =>
                    p.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && p.Password == password);

        public async Task<EventManager?> GetManagerByEmailAndPasswordAsync(string email, string password) =>
            (await _managerRepo.GetAllAsync())
                .FirstOrDefault(m =>
                    m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && m.Password == password);

        public async Task<Participant?> GetParticipantByEmailAsync(string email) =>
            (await _participantRepo.GetAllAsync())
                .FirstOrDefault(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        public async Task<EventManager?> GetManagerByEmailAsync(string email) =>
            (await _managerRepo.GetAllAsync())
                .FirstOrDefault(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        public async Task<EventHost?> GetHostByEmailAsync(string email) =>
            (await _hostRepo.GetAllAsync())
                .FirstOrDefault(h => h.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        public async Task<IEnumerable<EventHost>> SearchHostsByNameAsync(string name) =>
            (await _hostRepo.GetAllAsync())
                .Where(h => h.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        public async Task<IEnumerable<EventManager>> SearchManagersByNameAsync(string name) =>
            (await _managerRepo.GetAllAsync())
                .Where(m => m.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        public async Task<IEnumerable<Participant>> SearchParticipantsByNameAsync(string name) =>
            (await _participantRepo.GetAllAsync())
                .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        public async Task UpdateParticipantAsync(int id, string? name, string? email)
        {
            var p = await _participantRepo.GetByIdAsync(id);
            if (name  != null) p.Name  = name;
            if (email != null) p.Email = email;
            await _participantRepo.UpdateAsync(p);
        }

        public Task DeleteParticipantAsync(int id) => _participantRepo.DeleteAsync(id);
        public Task<IEnumerable<Participant>> GetAllParticipantsAsync() => _participantRepo.GetAllAsync();
        public Task<Participant> GetParticipantByIdAsync(int id) => _participantRepo.GetByIdAsync(id);

        public async Task<Participant?> TryGetParticipantByIdAsync(int id)
        {
            try { return await _participantRepo.GetByIdAsync(id); }
            catch { return null; }
        }

        public async Task<EventManager?> GetManagerByIdAsync(int id)
        {
            try { return await _managerRepo.GetByIdAsync(id); }
            catch { return null; }
        }

        public async Task<EventManager?> EnsureManagerRecordAsync(Participant participant)
        {
            var existing = await GetManagerByEmailAsync(participant.Email);
            if (existing != null) return existing;

            try
            {
                var allManagers = (await _managerRepo.GetAllAsync()).ToList();
                if (allManagers.Any(m => m.Id == participant.Id))
                    return null;

                var manager = new EventManager
                {
                    Id       = participant.Id,
                    Name     = participant.Name,
                    Email    = participant.Email,
                    Password = participant.Password
                };
                await _managerRepo.AddAsync(manager);
                return manager;
            }
            catch
            {
                return null;
            }
        }

        public async Task UpdateParticipantPreferencesAsync(int id, string? name, string? email, List<MailingPreference>? preferences)
        {
            var p = await _participantRepo.GetByIdAsync(id);
            if (name         != null) p.Name               = name;
            if (email        != null) p.Email              = email;
            if (preferences  != null) p.MailingPreferences = preferences;
            await _participantRepo.UpdateAsync(p);
        }
    }
}
