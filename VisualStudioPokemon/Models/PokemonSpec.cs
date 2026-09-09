namespace VisualStudioPokemon.Models
{
    public sealed class PokemonSpec(string species, string displayName, string nickname, PokemonSize size, bool shiny)
    {
        public string Species { get; private set; } = species;
        public string DisplayName { get; private set; } = displayName;
        public string Nickname { get; private set; } = nickname;
        public PokemonSize Size { get; private set; } = size;
        public bool Shiny { get; private set; } = shiny;
    }
}
