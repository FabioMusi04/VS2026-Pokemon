using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VisualStudioPokemon.Models;
using Path = System.IO.Path;

namespace VisualStudioPokemon.Services
{
    internal static class PokemonSpriteFactory
    {
        public static FrameworkElement Create(PokemonSpec spec)
        {
            double size = PokemonSizing.ToPixels(spec.Size);
            var host = new PokemonSpriteHost(spec, size);
            host.SetAnimation(PokemonAnimationState.Idle);
            return host;
        }

        public static string? FindSpritePath(PokemonSpec spec, PokemonAnimationState state)
        {
            string? speciesDirectory = FindSpeciesDirectory(spec);
            if (string.IsNullOrWhiteSpace(speciesDirectory) || !Directory.Exists(speciesDirectory))
            {
                return null;
            }

            string variant = spec.Shiny ? "shiny" : "default";
            string[] names = GetSpriteCandidates(variant, state);

            foreach (string name in names)
            {
                string path = Path.Combine(speciesDirectory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // If a shiny sprite is missing, fall back to the default variant.
            if (spec.Shiny)
            {
                foreach (string name in GetSpriteCandidates("default", state))
                {
                    string path = Path.Combine(speciesDirectory, name);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return Directory.EnumerateFiles(speciesDirectory, "*.gif", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        private static string[] GetSpriteCandidates(string variant, PokemonAnimationState state)
        {
            return state switch
            {
                PokemonAnimationState.WalkLeft =>
                                    [
                        variant + "_walk_left_8fps.gif",
                        variant + "_walk_8fps.gif",
                        variant + "_idle_8fps.gif"
                    ],
                PokemonAnimationState.WalkRight =>
                    [
                        variant + "_walk_8fps.gif",
                        variant + "_idle_8fps.gif"
                    ],
                _ =>
                    [
                        variant + "_idle_8fps.gif",
                        variant + "_walk_8fps.gif"
                    ],
            };
        }

        private static string? FindSpeciesDirectory(PokemonSpec spec)
        {
            string root = PokemonResourceLocator.ResourcesRoot;
            PokemonSpecies species = PokemonCatalog.Find(spec.Species);

            string preferred = Path.Combine(root, "gen" + species.Generation, spec.Species);
            if (Directory.Exists(preferred))
            {
                return preferred;
            }

            if (!Directory.Exists(root))
            {
                return null;
            }

            return Directory.GetDirectories(root, "gen*", SearchOption.TopDirectoryOnly)
                .Select(generationDirectory => Path.Combine(generationDirectory, spec.Species))
                .FirstOrDefault(Directory.Exists);
        }

        private sealed class PokemonSpriteHost : Grid, IPokemonSpriteView
        {
            private readonly PokemonSpec spec;
            private readonly AnimatedGifImage image;
            private string? currentPath;

            public PokemonSpriteHost(PokemonSpec spec, double size)
            {
                this.spec = spec;
                Width = size;
                Height = size;
                ClipToBounds = false;
                SnapsToDevicePixels = true;
                UseLayoutRounding = true;

                image = new AnimatedGifImage
                {
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                Children.Add(image);
            }

            public void SetAnimation(PokemonAnimationState state)
            {
                string? path = FindSpritePath(spec, state);

                if (path is null || string.IsNullOrWhiteSpace(path))
                {
                    image.RenderTransform = state == PokemonAnimationState.WalkLeft
                        ? new ScaleTransform(-1, 1)
                        : new ScaleTransform(1, 1);

                    return;
                }

                if (!StringComparer.OrdinalIgnoreCase.Equals(path, currentPath))
                {
                    currentPath = path;
                    image.SetImageFile(path);
                }

                bool hasNativeLeftFacingSprite =
                    path.IndexOf("_walk_left_", StringComparison.OrdinalIgnoreCase) >= 0;

                image.RenderTransform =
                    state == PokemonAnimationState.WalkLeft && !hasNativeLeftFacingSprite
                        ? new ScaleTransform(-1, 1)
                        : new ScaleTransform(1, 1);
            }
        }
    }
}
