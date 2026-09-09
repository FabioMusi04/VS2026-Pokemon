using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Services
{
    internal static class PokemonSizing
    {
        public static double ToPixels(PokemonSize size)
        {
            return size switch
            {
                PokemonSize.Nano => 40,
                PokemonSize.Small => 56,
                PokemonSize.Large => 104,
                PokemonSize.Medium => 78,
                _ => 78,
            };
        }
    }
}
