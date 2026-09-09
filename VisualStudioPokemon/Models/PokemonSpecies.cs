namespace VisualStudioPokemon.Models
{
    public sealed class PokemonSpecies(string key, string displayName, int generation)
    {
        public string Key { get; private set; } = key;
        public string DisplayName { get; private set; } = displayName;
        public int Generation { get; private set; } = generation;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
