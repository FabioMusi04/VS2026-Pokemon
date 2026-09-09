using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using VisualStudioPokemon.Models;

namespace VisualStudioPokemon.Options
{
    public sealed class PokemonOptionsPage : DialogPage
    {
        public static event EventHandler? OptionsApplied;

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

        [Category("General")]
        [DisplayName("Walk along the Visual Studio status bar")]
        [Description("Show Pokemon across the bottom of the main Visual Studio window instead of inside the Pokemon tool window. Movement is horizontal only.")]
        public bool WalkAlongVisualStudioStatusBar { get; set; }

        protected override void OnApply(DialogPage.PageApplyEventArgs e)
        {
            base.OnApply(e);
            OptionsApplied?.Invoke(this, EventArgs.Empty);
        }
    }
}
