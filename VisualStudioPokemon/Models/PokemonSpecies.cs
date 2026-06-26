namespace VisualStudioPokemon.Models
{
    public sealed class PokemonSpecies
    {
        public PokemonSpecies(string key, string displayName, int generation)
        {
            Key = key;
            DisplayName = displayName;
            Generation = generation;
        }

        public string Key { get; private set; }
        public string DisplayName { get; private set; }
        public int Generation { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
