using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VisualStudioPokemon.UI;

namespace VisualStudioPokemon.Commands
{
    internal sealed class PokemonCommands
    {
        private readonly VisualStudioPokemonPackage package;

        private PokemonCommands(VisualStudioPokemonPackage package, OleMenuCommandService commandService)
        {
            this.package = package;
            AddCommand(commandService, PackageIds.CmdStart, StartSession);
            AddCommand(commandService, PackageIds.CmdSpawnSelected, SpawnSelected);
            AddCommand(commandService, PackageIds.CmdSpawnRandom, SpawnRandom);
            AddCommand(commandService, PackageIds.CmdRemoveAll, RemoveAll);
            AddCommand(commandService, PackageIds.CmdRollCall, RollCall);
        }

        public static async Task InitializeAsync(VisualStudioPokemonPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new PokemonCommands(package, commandService);
            }
        }

        private static void AddCommand(OleMenuCommandService commandService, int id, EventHandler handler)
        {
            var commandId = new CommandID(PackageGuids.CmdSet, id);
            commandService.AddCommand(new MenuCommand(handler, commandId));
        }

        private void StartSession(object sender, EventArgs e)
        {
            _ = package.JoinableTaskFactory.RunAsync(async delegate
            {
                PokemonControl control = await package.ShowPokemonWindowAsync();
                control.EnsureStarted();
            });
        }

        private void SpawnSelected(object sender, EventArgs e)
        {
            _ = package.JoinableTaskFactory.RunAsync(async delegate
            {
                PokemonControl control = await package.ShowPokemonWindowAsync();
                control.SpawnSelectedFromToolbar();
            });
        }

        private void SpawnRandom(object sender, EventArgs e)
        {
            _ = package.JoinableTaskFactory.RunAsync(async delegate
            {
                PokemonControl control = await package.ShowPokemonWindowAsync();
                control.SpawnRandom();
            });
        }

        private void RemoveAll(object sender, EventArgs e)
        {
            _ = package.JoinableTaskFactory.RunAsync(async delegate
            {
                PokemonControl control = await package.ShowPokemonWindowAsync();
                control.RemoveAll();
            });
        }

        private void RollCall(object sender, EventArgs e)
        {
            _ = package.JoinableTaskFactory.RunAsync(async delegate
            {
                PokemonControl control = await package.ShowPokemonWindowAsync();
                control.RollCall();
            });
        }
    }
}
