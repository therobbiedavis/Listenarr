using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Listenarr.Api.Controllers;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class RootFoldersControllerTests
    {
        private class FakeService : IRootFolderService
        {
            public List<RootFolder> Store { get; } = new List<RootFolder>();

            public Task<List<RootFolder>> GetAllAsync() => Task.FromResult(new List<RootFolder>(Store));

            public Task<RootFolder?> GetByIdAsync(int id)
            {
                var f = Store.Find(s => s.Id == id);
                return Task.FromResult<RootFolder?>(f);
            }

            public Task<RootFolder> CreateAsync(RootFolder root)
            {
                // simulate duplicate path error
                if (Store.Exists(s => string.Equals(s.Path, root.Path, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("A root with the same path already exists")
;                root.Id = Store.Count + 1;
                Store.Add(root);
                return Task.FromResult(root);
            }

            public Task<RootFolder> UpdateAsync(RootFolder root, bool moveFiles = false, bool deleteEmptySource = true)
            {
                var idx = Store.FindIndex(s => s.Id == root.Id);
                if (idx < 0) throw new KeyNotFoundException("Root folder not found");

                // simulate invalid operation for certain paths
                if (root.Path?.Contains("/invalid/") == true) throw new InvalidOperationException("Invalid path")
;
                Store[idx] = root;
                return Task.FromResult(root);
            }

            public Task DeleteAsync(int id, int? reassignRootId = null)
            {
                var idx = Store.FindIndex(s => s.Id == id);
                if (idx < 0) throw new KeyNotFoundException("Root folder not found");

                // simulate in-use error if path contains "inuse"
                if (Store[idx].Path?.Contains("inuse") == true && reassignRootId == null)
                    throw new InvalidOperationException("Root folder in use")
;
                Store.RemoveAt(idx);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task GetAll_ReturnsAll()
        {
            var svc = new FakeService();
            svc.Store.AddRange(new[] {
                new RootFolder { Id = 1, Name = "Root1", Path = "C:/root1" },
                new RootFolder { Id = 2, Name = "Root2", Path = "D:/root2" }
            });

            var controller = new RootFoldersController(svc);

            var res = await controller.GetAll();
            var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(res);
            var list = Assert.IsAssignableFrom<List<RootFolder>>(ok.Value);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task Get_NotFound_Returns404()
        {
            var svc = new FakeService();
            var controller = new RootFoldersController(svc);

            var res = await controller.Get(123);
            var notFound = Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(res);
            Assert.Contains("not found", notFound.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Create_DuplicatePath_ReturnsBadRequest()
        {
            var svc = new FakeService();
            svc.Store.Add(new RootFolder { Id = 1, Name = "R1", Path = "C:/dup" });
            var controller = new RootFoldersController(svc);

            var req = new RootFolder { Name = "New", Path = "C:/dup" };
            var res = await controller.Create(req);

            var bad = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(res);
            Assert.Contains("same path", bad.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_IdMismatch_ReturnsBadRequest()
        {
            var svc = new FakeService();
            var controller = new RootFoldersController(svc);

            var req = new RootFolder { Id = 2, Name = "R", Path = "C:/p" };
            var res = await controller.Update(1, req);

            var bad = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(res);
            Assert.Contains("Id mismatch", bad.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_NotFound_ReturnsNotFound()
        {
            var svc = new FakeService();
            var controller = new RootFoldersController(svc);

            var req = new RootFolder { Id = 99, Name = "R", Path = "C:/p" };
            var res = await controller.Update(99, req);

            var nf = Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(res);
            Assert.Contains("not found", nf.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Delete_InUseWithoutReassign_ReturnsBadRequest()
        {
            var svc = new FakeService();
            svc.Store.Add(new RootFolder { Id = 1, Name = "R", Path = "C:/inuse" });
            var controller = new RootFoldersController(svc);

            var res = await controller.Delete(1, null);
            var bad = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(res);
            Assert.Contains("in use", bad.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Delete_WithReassign_Succeeds()
        {
            var svc = new FakeService();
            svc.Store.Add(new RootFolder { Id = 1, Name = "R", Path = "C:/inuse" });
            svc.Store.Add(new RootFolder { Id = 2, Name = "R2", Path = "D:/r" });
            var controller = new RootFoldersController(svc);

            var res = await controller.Delete(1, 2);
            var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(res);
            Assert.Contains("Deleted", ok.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
