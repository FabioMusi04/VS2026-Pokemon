using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Services
{
    internal static class PokemonSizing
    {
        public static double ToPixels(PokemonSize size)
        {
            switch (size)
            {
                case PokemonSize.Nano:
                    return 40;
                case PokemonSize.Small:
                    return 56;
                case PokemonSize.Large:
                    return 104;
                default:
                    return 78;
            }
        }
    }
}
