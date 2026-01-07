using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Api.Repositories;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class RootFolderService : IRootFolderService
    {
        private readonly IRootFolderRepository _repo;
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly ILogger<RootFolderService> _logger;
        private readonly IMoveQueueService? _moveQueue;

        public RootFolderService(IRootFolderRepository repo, IDbContextFactory<ListenArrDbContext> dbFactory, ILogger<RootFolderService> logger, IMoveQueueService? moveQueue = null)
        {
            _repo = repo;
            _dbFactory = dbFactory;
            _logger = logger;
            _moveQueue = moveQueue;
        }

        public async Task<RootFolder> CreateAsync(RootFolder root)
        {
            // Normalize path
            root.Path = root.Path?.Trim() ?? string.Empty;
            root.Name = root.Name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(root.Path)) throw new ArgumentException("Path is required");
            if (string.IsNullOrWhiteSpace(root.Name)) throw new ArgumentException("Name is required");

            var existingByPath = await _repo.GetByPathAsync(root.Path);
            if (existingByPath != null) throw new InvalidOperationException("A root folder with that path already exists.");

            // If this is set as default, clear other defaults
            if (root.IsDefault)
            {
                using var ctx = await _dbFactory.CreateDbContextAsync();
                var others = ctx.RootFolders.Where(r => r.IsDefault).ToList();
                foreach (var o in others)
                {
                    o.IsDefault = false;
                }
                ctx.RootFolders.UpdateRange(others);
                await ctx.SaveChangesAsync();
            }

            await _repo.AddAsync(root);
            return root;
        }

        public async Task DeleteAsync(int id, int? reassignRootId = null)
        {
            using var ctx = await _dbFactory.CreateDbContextAsync();
            var root = await ctx.RootFolders.FindAsync(id);
            if (root == null) throw new KeyNotFoundException("Root folder not found");

            // Check for referenced audiobooks
            var referenced = ctx.Audiobooks.Any(a => a.BasePath != null && (a.BasePath == root.Path || a.BasePath.StartsWith(root.Path + System.IO.Path.DirectorySeparatorChar)));
            if (referenced && !reassignRootId.HasValue)
            {
                throw new InvalidOperationException("Root folder is in use by audiobooks; reassign before deletion or provide reassignRootId.");
            }

            if (referenced && reassignRootId.HasValue)
            {
                var newRoot = await ctx.RootFolders.FindAsync(reassignRootId.Value);
                if (newRoot == null) throw new KeyNotFoundException("Reassign root not found");
                // Reassign audiobooks that start with old path to new root path
                var affected = ctx.Audiobooks.Where(a => a.BasePath != null && (a.BasePath == root.Path || a.BasePath.StartsWith(root.Path + System.IO.Path.DirectorySeparatorChar))).ToList();
                foreach (var a in affected)
                {
                    // Replace prefix
                    if (a.BasePath == root.Path) a.BasePath = newRoot.Path;
                    else if (a.BasePath!.StartsWith(root.Path + System.IO.Path.DirectorySeparatorChar))
                    {
                        a.BasePath = newRoot.Path + a.BasePath.Substring(root.Path.Length);
                    }
                }
                ctx.Audiobooks.UpdateRange(affected);
                await ctx.SaveChangesAsync();
            }

            await _repo.RemoveAsync(id);
        }

        public async Task<List<RootFolder>> GetAllAsync() => await _repo.GetAllAsync();

        public async Task<RootFolder?> GetByIdAsync(int id) => await _repo.GetByIdAsync(id);

        public async Task<RootFolder> UpdateAsync(RootFolder root, bool moveFiles = false, bool deleteEmptySource = true)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            root.Path = root.Path?.Trim() ?? string.Empty;
            root.Name = root.Name?.Trim() ?? string.Empty;

            var existing = await _repo.GetByIdAsync(root.Id);
            if (existing == null) throw new KeyNotFoundException("Root folder not found");

            if (!string.Equals(existing.Path, root.Path, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _repo.GetByPathAsync(root.Path);
                if (duplicate != null && duplicate.Id != root.Id) throw new InvalidOperationException("Another root folder with that path already exists.");
            }

            // Update default handling
            if (root.IsDefault)
            {
                using var ctx = await _dbFactory.CreateDbContextAsync();
                var others = ctx.RootFolders.Where(r => r.IsDefault && r.Id != root.Id).ToList();
                foreach (var o in others)
                {
                    o.IsDefault = false;
                }
                ctx.RootFolders.UpdateRange(others);
                await ctx.SaveChangesAsync();
            }

            // Store values from the existing entity before opening a new context
            var oldPath = existing.Path;
            var newPath = root.Path;

            // Update root folder and audiobooks in the same context to ensure transaction consistency
            using (var ctx = await _dbFactory.CreateDbContextAsync())
            {
                // Re-load the entity in this context so it's properly tracked
                var trackedRoot = await ctx.RootFolders.FindAsync(root.Id);
                if (trackedRoot == null) throw new KeyNotFoundException("Root folder not found");

                // Apply updates to the tracked entity
                trackedRoot.Name = root.Name;
                trackedRoot.Path = root.Path;
                trackedRoot.IsDefault = root.IsDefault;
                trackedRoot.UpdatedAt = DateTime.UtcNow;

                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Only include audiobooks that are subdirectories of the root, not the root itself
                    var affected = ctx.Audiobooks.Where(a => a.BasePath != null && a.BasePath.StartsWith(oldPath + System.IO.Path.DirectorySeparatorChar)).ToList();
                    // Record original and new paths before updating DB so we can enqueue moves if requested
                    var moves = new List<(int audiobookId, string original, string target)>();

                    foreach (var a in affected)
                    {
                        var original = a.BasePath!;
                        string target;
                        // Replace the old root prefix with the new root prefix
                        target = newPath + original.Substring(oldPath.Length);

                        moves.Add((a.Id, original, target));
                        a.BasePath = target;
                    }

                    ctx.Audiobooks.UpdateRange(affected);
                    
                    // Save both root folder and audiobook updates in one transaction
                    await ctx.SaveChangesAsync();

                    if (moveFiles && _moveQueue != null)
                    {
                        foreach (var m in moves)
                        {
                            try
                            {
                                _ = _moveQueue.EnqueueMoveAsync(m.audiobookId, m.target, m.original);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to enqueue move for audiobook {AudiobookId} during root rename", m.audiobookId);
                            }
                        }
                    }
                }
                else
                {
                    // Just update the root folder if path hasn't changed
                    await ctx.SaveChangesAsync();
                }

                // Return the tracked entity (or re-load existing with updated values)
                existing.Name = trackedRoot.Name;
                existing.Path = trackedRoot.Path;
                existing.IsDefault = trackedRoot.IsDefault;
                existing.UpdatedAt = trackedRoot.UpdatedAt;
            }

            return existing;
        }
    }
}