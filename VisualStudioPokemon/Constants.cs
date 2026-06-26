using System;

namespace VisualStudioPokemon
{
    internal static class PackageGuids
    {
        public const string PackageGuidString = "b6608e4b-4dc4-4aa7-b2ad-f64213d2242e";
        public const string CmdSetGuidString = "1b1d9d25-cd9a-43e2-a4fd-646d0c9f8dbe";
        public static readonly Guid CmdSet = new Guid(CmdSetGuidString);
    }

    internal static class PackageIds
    {
        public const int PokemonMenuGroup = 0x1020;
        public const int CmdStart = 0x0100;
        public const int CmdSpawnSelected = 0x0101;
        public const int CmdSpawnRandom = 0x0102;
        public const int CmdRemoveAll = 0x0103;
        public const int CmdRollCall = 0x0104;
    }
}
