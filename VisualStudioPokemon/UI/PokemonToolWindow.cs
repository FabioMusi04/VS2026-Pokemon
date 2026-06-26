using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VisualStudioPokemon.UI
{
    [Guid("4cc3c4b2-75f8-4e41-a7d4-15c3f7c5b17a")]
    public sealed class PokemonToolWindow : ToolWindowPane
    {
        public PokemonToolWindow() : base(null)
        {
            Caption = "Visual Studio Pokemon";
            Content = new PokemonControl();
        }
    }
}
