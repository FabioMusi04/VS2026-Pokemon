using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VisualStudioPokemon.Commands;
using VisualStudioPokemon.Options;
using VisualStudioPokemon.UI;

namespace VisualStudioPokemon
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(PokemonToolWindow))]
    [ProvideOptionPage(typeof(PokemonOptionsPage), "Visual Studio Pokemon", "General", 0, 0, true)]
    [Guid(VisualStudioPokemonPackage.PackageGuidString)]
    public sealed class VisualStudioPokemonPackage : AsyncPackage
    {
        /// <summary>
        /// VisualStudioPokemonPackage GUID string.
        /// </summary>
        public const string PackageGuidString = PackageGuids.PackageGuidString;

        internal static VisualStudioPokemonPackage? Instance { get; private set; }

        #region Package Members

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Instance = this;
            await PokemonCommands.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }

            base.Dispose(disposing);
        }

        internal async Task<PokemonControl> ShowPokemonWindowAsync(CancellationToken cancellationToken = default)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            ToolWindowPane window = FindToolWindow(typeof(PokemonToolWindow), 0, true);
            if (window == null || window.Frame == null)
            {
                throw new NotSupportedException("Cannot create the Visual Studio Pokemon tool window.");
            }

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());

            return window.Content as PokemonControl;
        }

        internal PokemonOptionsPage Options
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return (PokemonOptionsPage)GetDialogPage(typeof(PokemonOptionsPage));
            }
        }

        #endregion
    }
}
