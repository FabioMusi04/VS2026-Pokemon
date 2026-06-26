using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Services
{
    internal static class PokemonCatalog
    {
        private static readonly Random Random = new Random();
        private static readonly Lazy<IReadOnlyList<PokemonSpecies>> ItemsLazy = new Lazy<IReadOnlyList<PokemonSpecies>>(LoadSpeciesFromResources);

        private static readonly string[] RandomNames = new[]
        {
            "Bella", "Charlie", "Molly", "Coco", "Ruby", "Oscar", "Lucy", "Bailey", "Milo", "Daisy",
            "Archie", "Ollie", "Rosie", "Lola", "Frankie", "Roxy", "Poppy", "Luna", "Jack", "Millie",
            "Teddy", "Cooper", "Bear", "Rocky", "Alfie", "Hugo", "Bonnie", "Pepper", "Lily", "Tilly",
            "Leo", "Maggie", "George", "Mia", "Marley", "Harley", "Chloe", "Lulu", "Missy", "Jasper",
            "Billy", "Nala", "Monty", "Ziggy", "Winston", "Zeus", "Zoe", "Stella", "Sasha", "Rusty",
            "Gus", "Baxter", "Dexter", "Willow", "Barney", "Bruno", "Penny", "Honey", "Milly", "Murphy",
            "Simba", "Holly", "Benji", "Henry", "Lilly", "Pippa", "Shadow", "Sam", "Lucky", "Ellie",
            "Duke", "Jessie", "Cookie", "Harvey", "Bruce", "Jax", "Rex", "Louie", "Jet", "Banjo"
        };

        public static IReadOnlyList<PokemonSpecies> All
        {
            get { return ItemsLazy.Value; }
        }

        public static PokemonSpecies Find(string key)
        {
            return All.FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Key, key)) ?? All[0];
        }

        public static PokemonSpecies GetRandomSpecies()
        {
            IReadOnlyList<PokemonSpecies> items = All;
            return items[Random.Next(items.Count)];
        }

        public static string GetRandomName()
        {
            return RandomNames[Random.Next(RandomNames.Length)];
        }

        private static IReadOnlyList<PokemonSpecies> LoadSpeciesFromResources()
        {
            string root = PokemonResourceLocator.ResourcesRoot;
            var byKey = new Dictionary<string, PokemonSpecies>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(root))
            {
                foreach (string generationDirectory in Directory.GetDirectories(root, "gen*", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                {
                    int generation = ParseGeneration(generationDirectory);
                    foreach (string pokemonDirectory in Directory.GetDirectories(generationDirectory).OrderBy(x => x))
                    {
                        string key = Path.GetFileName(pokemonDirectory);
                        if (String.IsNullOrWhiteSpace(key) || byKey.ContainsKey(key) || !HasUsableSprite(pokemonDirectory))
                        {
                            continue;
                        }

                        byKey.Add(key, new PokemonSpecies(key, ToDisplayName(key), generation));
                    }
                }
            }

            IReadOnlyList<PokemonSpecies> loaded = byKey.Values
                .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Generation)
                .ToList();

            if (loaded.Count > 0)
            {
                return loaded;
            }

            // Last-resort fallback so the extension still starts if Resources are not copied.
            return new[]
            {
                new PokemonSpecies("bulbasaur", "Bulbasaur", 1),
                new PokemonSpecies("charmander", "Charmander", 1),
                new PokemonSpecies("squirtle", "Squirtle", 1),
                new PokemonSpecies("pikachu", "Pikachu", 1)
            };
        }

        private static bool HasUsableSprite(string pokemonDirectory)
        {
            return Directory.EnumerateFiles(pokemonDirectory, "*_idle_8fps.gif", SearchOption.TopDirectoryOnly).Any()
                || Directory.EnumerateFiles(pokemonDirectory, "*_walk_8fps.gif", SearchOption.TopDirectoryOnly).Any()
                || Directory.EnumerateFiles(pokemonDirectory, "*_walk_left_8fps.gif", SearchOption.TopDirectoryOnly).Any();
        }

        private static int ParseGeneration(string generationDirectory)
        {
            string name = Path.GetFileName(generationDirectory) ?? String.Empty;
            if (name.StartsWith("gen", StringComparison.OrdinalIgnoreCase))
            {
                int value;
                if (Int32.TryParse(name.Substring(3), out value))
                {
                    return value;
                }
            }

            return 0;
        }

        private static string ToDisplayName(string key)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(key, "nidoran_female"))
            {
                return "Nidoran♀";
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(key, "nidoran_male"))
            {
                return "Nidoran♂";
            }

            string text = key.Replace('_', ' ').Replace('-', ' ');
            text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
            text = text.Replace(" Mr ", " Mr. ").Replace(" Jr ", " Jr.");
            return text;
        }
    }
}
