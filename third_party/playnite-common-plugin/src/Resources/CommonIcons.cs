using Playnite;

namespace CommonPlugin.Resources
{
    public static class CommonIcons
    {
        public static string Repair => "e20f";
        public static string Move => "f0252";
        public static string Install => "f019";
        public static string Uninstall => "f19d6";
        public static string FinishInstallation => "f05d";
        public static string Update => "f021";
        public static string ImportGame => "e27d";
        public static string Dlcs => "f12e";

        public static string IcoInstall => "ef07";
        public static string Reload => "efd1";
        public static string Cancel => "ec4f";
        public static string SelectFolder => "ec5b";
        public static string IcoRepair => "efd2";
        public static string CheckAll => "eed9";

        public static UIIcon RepairIcon { get; } = UIIcon.FromFontIcon(Repair, Fonts.NerdFont);
        public static UIIcon MoveIcon { get; } = UIIcon.FromFontIcon(Move, Fonts.NerdFont);
        public static UIIcon InstallIcon { get; } = UIIcon.FromFontIcon(Install, Fonts.NerdFont);
        public static UIIcon UninstallIcon { get; } = UIIcon.FromFontIcon(Uninstall, Fonts.NerdFont);
        public static UIIcon ExtrasIcon { get; } = UIIcon.FromFontIcon("ef3c", Fonts.IcoFont);
        public static UIIcon FinishInstallationIcon { get; } = UIIcon.FromFontIcon(FinishInstallation, Fonts.NerdFont);
        public static UIIcon UpdateIcon { get; } = UIIcon.FromFontIcon(Update, Fonts.NerdFont);
        public static UIIcon ImportGameIcon { get; } = UIIcon.FromFontIcon(ImportGame, Fonts.NerdFont);
        public static UIIcon DlcsIcon { get; } = UIIcon.FromFontIcon(Dlcs, Fonts.NerdFont);
    }
}