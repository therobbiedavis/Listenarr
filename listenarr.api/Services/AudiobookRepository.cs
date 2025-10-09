/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public class AudiobookRepository : IAudiobookRepository
    {
        private readonly ListenArrDbContext _db;
        public AudiobookRepository(ListenArrDbContext db)
        {
            _db = db;
        }

        public async Task<List<Audiobook>> GetAllAsync()
        {
            return await _db.Audiobooks.OrderBy(a => a.Title).ToListAsync();
        }

        public async Task<Audiobook?> GetByAsinAsync(string asin)
        {
            return await _db.Audiobooks.FirstOrDefaultAsync(a => a.Asin == asin);
        }

        public async Task<Audiobook?> GetByIsbnAsync(string isbn)
        {
            return await _db.Audiobooks.FirstOrDefaultAsync(a => a.Isbn == isbn);
        }

        public async Task<Audiobook?> GetByIdAsync(int id)
        {
            return await _db.Audiobooks
                .Include(a => a.QualityProfile)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Add(audiobook);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Update(audiobook);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Remove(audiobook);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByIdAsync(int id)
        {
            var audiobook = await GetByIdAsync(id);
            if (audiobook == null)
                return false;

            return await DeleteAsync(audiobook);
        }

        public async Task<int> DeleteBulkAsync(List<int> ids)
        {
            var audiobooks = await _db.Audiobooks
                .Where(a => ids.Contains(a.Id))
                .ToListAsync();

            if (!audiobooks.Any())
                return 0;

            _db.Audiobooks.RemoveRange(audiobooks);
            await _db.SaveChangesAsync();
            return audiobooks.Count;
        }
    }
}
