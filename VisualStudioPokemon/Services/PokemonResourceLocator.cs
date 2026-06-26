using System;
using System.IO;
using System.Reflection;

namespace VisualStudioPokemon.Services
{
    internal static class PokemonResourceLocator
    {
        private static readonly Lazy<string> ResourcesRootLazy = new Lazy<string>(FindResourcesRoot);

        public static string ResourcesRoot
        {
            get { return ResourcesRootLazy.Value; }
        }

        public static string GetResourcePath(params string[] parts)
        {
            string path = ResourcesRoot;
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }
            return path;
        }

        private static string FindResourcesRoot()
        {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;

            string[] directCandidates = new[]
            {
                Path.Combine(assemblyFolder, "Resources"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources")
            };

            foreach (string candidate in directCandidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            // Useful while debugging from bin folders: walk upward until a Resources folder is found.
            DirectoryInfo current = new DirectoryInfo(assemblyFolder);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, "Resources");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return directCandidates[0];
        }
    }
}
