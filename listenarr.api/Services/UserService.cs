using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Listenarr.Api.Services
{
    public interface IUserService
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User> CreateUserAsync(string username, string password, string? email = null, bool isAdmin = false);
        Task UpdatePasswordAsync(string username, string newPassword);
        Task<bool> ValidateCredentialsAsync(string username, string password);
        Task<List<User>> GetAdminUsersAsync();
    }

    public class UserService : IUserService
    {
        private readonly ListenArrDbContext _db;
        private readonly ILogger<UserService> _logger;

        public UserService(ListenArrDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User> CreateUserAsync(string username, string password, string? email = null, bool isAdmin = false)
        {
            var existing = await GetByUsernameAsync(username);
            if (existing != null) throw new InvalidOperationException("User already exists");

            var hash = HashPassword(password);
            var user = new User
            {
                Username = username,
                PasswordHash = hash,
                Email = email,
                IsAdmin = isAdmin
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            var user = await GetByUsernameAsync(username);
            if (user == null) return false;
            return VerifyPassword(password, user.PasswordHash);
        }

        public async Task UpdatePasswordAsync(string username, string newPassword)
        {
            var user = await GetByUsernameAsync(username);
            if (user == null) throw new InvalidOperationException("User not found");
            user.PasswordHash = HashPassword(newPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<List<User>> GetAdminUsersAsync()
        {
            return await _db.Users.Where(u => u.IsAdmin).ToListAsync();
        }

        // PBKDF2 with HMACSHA256
        private static string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        private static bool VerifyPassword(string password, string stored)
        {
            try
            {
                var parts = stored.Split(':', 2);
                if (parts.Length != 2) return false;
                var salt = Convert.FromBase64String(parts[0]);
                var hash = Convert.FromBase64String(parts[1]);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(hash.Length);
                return CryptographicOperations.FixedTimeEquals(computed, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}
