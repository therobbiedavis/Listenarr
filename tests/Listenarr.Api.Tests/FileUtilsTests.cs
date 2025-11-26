using System;
using System.IO;
using System.Collections.Generic;
using Xunit;
using Listenarr.Api.Services;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Listenarr.Api.Tests
{
    public class FileUtilsTests
    {
        [Fact]
        public void GetUniqueDestinationPath_ReturnsSameIfNotExists()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "fu-test-" + Guid.NewGuid().ToString() + ".txt");
            // Ensure it does not exist
            if (File.Exists(tmp)) File.Delete(tmp);

            var result = FileUtils.GetUniqueDestinationPath(tmp);
            Assert.Equal(tmp, result);
        }

        [Fact]
        public void GetUniqueDestinationPath_AppendsSuffixWhenExists()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fu-dir-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "file.txt");
            File.WriteAllText(file, "x");

            var result = FileUtils.GetUniqueDestinationPath(file);
            Assert.NotEqual(file, result);
            Assert.StartsWith(Path.Combine(dir, "file (") , result);

            // cleanup
            try { Directory.Delete(dir, true); } catch { }
        }

        [Fact]
        public void GetUniqueDestinationPath_RespectsInMemoryUsedSet()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fu-dir-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            var desired = Path.Combine(dir, "dup.mp3");
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { desired };

            var result = FileUtils.GetUniqueDestinationPath(desired, File.Exists, used);
            Assert.NotEqual(desired, result);
            Assert.Contains("dup (", result);

            try { Directory.Delete(dir, true); } catch { }
        }

        [Fact]
        public void GetUniqueDestinationPath_UsesCustomExistsPredicate()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "fu-test-" + Guid.NewGuid() + ".bin");
            // pretend only the original path exists by using a predicate that returns true
            // only for the original desired path. This ensures the generator can find a
            // candidate that does not exist according to the predicate.
            bool ExistsPredicate(string p) => string.Equals(p, tmp, StringComparison.OrdinalIgnoreCase);

            var result = FileUtils.GetUniqueDestinationPath(tmp, ExistsPredicate, null);
            Assert.NotEqual(tmp, result);
            Assert.Contains(" (1)", result);
        }

        [Fact]
        public void GetUniqueDestinationPath_LongName_AppendsSuffix()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fu-long-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);

            // Create a long filename (but within typical filesystem limits)
            var longName = new string('a', 180) + ".mp3";
            var path = Path.Combine(dir, longName);
            File.WriteAllText(path, "x");

            var result = FileUtils.GetUniqueDestinationPath(path);
            Assert.NotEqual(path, result);
            Assert.Contains(" (1)", result);

            try { Directory.Delete(dir, true); } catch { }
        }

        [Fact]
        public void GetUniqueDestinationPath_InvalidPredicate_ThrowsHandled_ReturnsOriginal()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "fu-test-ex" + Guid.NewGuid() + ".dat");
            bool BadPredicate(string p) => throw new InvalidOperationException("boom");

            var result = FileUtils.GetUniqueDestinationPath(tmp, BadPredicate, null);
            // On predicate exception the helper should fall back to returning the original desired path
            Assert.Equal(tmp, result);
        }

        [Fact]
        public void GetUniqueDestinationPath_ReadOnlyDirectory_AppendsSuffix()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fu-ro-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "exists.mp3");
            File.WriteAllText(file, "x");

            // Make directory read-only to simulate permission edge-case
            var dirInfo = new DirectoryInfo(dir);
            var origAttr = dirInfo.Attributes;
            try
            {
                dirInfo.Attributes |= FileAttributes.ReadOnly;

                var result = FileUtils.GetUniqueDestinationPath(file);
                Assert.NotEqual(file, result);
                Assert.Contains(" (1)", result);
            }
            finally
            {
                try { dirInfo.Attributes = origAttr; } catch { }
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GetUniqueDestinationPath_WriteDeniedByAcl_OnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                // Not applicable on non-Windows platforms in this test
                return;
            }

            var dir = Path.Combine(Path.GetTempPath(), "fu-acl-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            var desired = Path.Combine(dir, "blocked.mp3");
            // Create an existing file to force suffixing
            var existing = Path.Combine(dir, "blocked.mp3");
            File.WriteAllText(existing, "x");

            var dirInfo = new DirectoryInfo(dir);
            var originalSecurity = dirInfo.GetAccessControl();

            try
            {
                // Deny write permission for the current user
                var currentUser = WindowsIdentity.GetCurrent()?.User;
                if (currentUser == null)
                {
                    return; // can't determine user, skip
                }

                var rule = new FileSystemAccessRule(currentUser, FileSystemRights.CreateFiles | FileSystemRights.Write, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny);
                var security = dirInfo.GetAccessControl();
                security.AddAccessRule(rule);
                dirInfo.SetAccessControl(security);

                // Generate unique path
                var result = FileUtils.GetUniqueDestinationPath(desired);

                // Attempt to write to the result path - should throw UnauthorizedAccessException when ACL denies write
                bool threw = false;
                try
                {
                    File.WriteAllText(result, "data");
                }
                catch (UnauthorizedAccessException)
                {
                    threw = true;
                }

                Assert.True(threw, "Expected UnauthorizedAccessException when writing to path in ACL-denied directory");
            }
            finally
            {
                try { dirInfo.SetAccessControl(originalSecurity); } catch { }
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
