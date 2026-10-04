#nullable disable
using System;
using System.IO;

namespace PagaParaMorir.Escrow.Tests
{
    internal static class RepoRoot
    {
        public static readonly string Path = Find();

        private static string Find()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Anchor.toml")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repo (Anchor.toml).");
        }
    }
}
