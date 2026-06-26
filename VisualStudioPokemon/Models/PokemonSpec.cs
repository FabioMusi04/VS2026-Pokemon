namespace VisualStudioPokemon.Models
{
    public sealed class PokemonSpec
    {
        public PokemonSpec(string species, string displayName, string nickname, PokemonSize size, bool shiny)
        {
            Species = species;
            DisplayName = displayName;
            Nickname = nickname;
            Size = size;
            Shiny = shiny;
        }

        public string Species { get; private set; }
        public string DisplayName { get; private set; }
        public string Nickname { get; private set; }
        public PokemonSize Size { get; private set; }
        public bool Shiny { get; private set; }
    }
}
