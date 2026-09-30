using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityIsekaiGame.Editor
{
    internal sealed class BuildOutputTransaction : IDisposable
    {
        private readonly string finalDirectory;
        private readonly string stagingDirectory;
        private readonly string backupDirectory;
        private bool committed;

        public BuildOutputTransaction(string finalExecutablePath)
        {
            string executableName = Path.GetFileName(finalExecutablePath);
            finalDirectory = Path.GetDirectoryName(finalExecutablePath)
                ?? throw new InvalidOperationException("The build output directory is invalid.");
            string parent = Path.GetDirectoryName(finalDirectory)
                ?? throw new InvalidOperationException("The build output parent directory is invalid.");
            string leaf = Path.GetFileName(finalDirectory);
            string suffix = Guid.NewGuid().ToString("N");
            stagingDirectory = Path.Combine(parent, $".{leaf}.staging-{suffix}");
            backupDirectory = Path.Combine(parent, $".{leaf}.previous-{suffix}");
            Directory.CreateDirectory(stagingDirectory);
            StagingExecutablePath = Path.Combine(stagingDirectory, executableName);
        }

        public string StagingExecutablePath { get; }

        public void ValidateManagedAssemblies(IEnumerable<string> forbiddenAssemblyNames)
        {
            string managed = Directory.GetDirectories(stagingDirectory, "*_Data", SearchOption.TopDirectoryOnly)
                .Select(path => Path.Combine(path, "Managed"))
                .SingleOrDefault(Directory.Exists)
                ?? throw new InvalidOperationException("The staged client build has no Managed assembly directory.");
            string[] present = forbiddenAssemblyNames.Where(name => File.Exists(Path.Combine(managed, name))).ToArray();
            if (present.Length > 0)
                throw new InvalidOperationException("The client build contains forbidden server assemblies: " + string.Join(", ", present));
        }

        public void Commit()
        {
            bool movedExisting = false;
            try
            {
                if (Directory.Exists(finalDirectory))
                {
                    Directory.Move(finalDirectory, backupDirectory);
                    movedExisting = true;
                }

                Directory.Move(stagingDirectory, finalDirectory);
                committed = true;
                if (movedExisting) Directory.Delete(backupDirectory, true);
            }
            catch
            {
                if (!Directory.Exists(finalDirectory) && movedExisting && Directory.Exists(backupDirectory))
                    Directory.Move(backupDirectory, finalDirectory);
                throw;
            }
        }

        public void Dispose()
        {
            if (!committed && Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, true);
            if (committed && Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
        }
    }
}
