using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Options
{
    public sealed class PokemonOptionsPage : DialogPage
    {
        [Category("General")]
        [DisplayName("Default size")]
        [Description("Default size used by newly spawned companions.")]
        public PokemonSize DefaultSize { get; set; } = PokemonSize.Medium;

        [Category("General")]
        [DisplayName("Shiny odds")]
        [Description("One-in-N chance for Random to create a shiny companion. Use 1 to force shiny.")]
        public int ShinyOdds { get; set; } = 8192;

        [Category("General")]
        [DisplayName("Restore previous companions")]
        [Description("Reload companions from the previous Visual Studio session.")]
        public bool RestorePreviousCompanions { get; set; } = true;
    }
}
