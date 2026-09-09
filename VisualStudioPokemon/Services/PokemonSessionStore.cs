using System;
using System.Collections.Generic;
using System.IO;
using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Services
{
    internal sealed class PokemonSessionStore
    {
        private readonly string filePath;

        public PokemonSessionStore()
        {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisualStudioPokemon");
            Directory.CreateDirectory(basePath);
            filePath = Path.Combine(basePath, "pokemon-session.txt");
        }

        public IReadOnlyList<PokemonSpec> Load()
        {
            var result = new List<PokemonSpec>();
            if (!File.Exists(filePath))
            {
                return result;
            }

            foreach (string line in File.ReadAllLines(filePath))
            {
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split('|');
                if (parts.Length < 5)
                {
                    continue;
                }

                string species = Uri.UnescapeDataString(parts[0]);
                string displayName = Uri.UnescapeDataString(parts[1]);
                string nickname = Uri.UnescapeDataString(parts[2]);

                Enum.TryParse(parts[3], out PokemonSize size);
                Boolean.TryParse(parts[4], out bool shiny);

                result.Add(new PokemonSpec(species, displayName, nickname, size, shiny));
            }

            return result;
        }

        public void Save(IEnumerable<PokemonSpec> specs)
        {
            var lines = new List<string>();
            foreach (PokemonSpec spec in specs)
            {
                lines.Add(String.Join("|",
                    Uri.EscapeDataString(spec.Species ?? String.Empty),
                    Uri.EscapeDataString(spec.DisplayName ?? String.Empty),
                    Uri.EscapeDataString(spec.Nickname ?? String.Empty),
                    spec.Size.ToString(),
                    spec.Shiny.ToString()));
            }

            File.WriteAllLines(filePath, lines);
        }
    }
}
