using System;
using System.Collections.Generic;

namespace LMDTool
{
    public enum AppLanguage
    {
        PtBr,
        EnUs
    }

    /*
        Minimal static localization helper for the tool's own UI strings
        (buttons, labels, log messages). This has nothing to do with the
        game text being translated by the parsers - it only covers the
        chrome around it (e.g. "Export", "Import", "Files found").

        Usage:
            Localization.T("export")  -> "Exportar → TXT" or "Export → TXT"

        To add a new language:
            1. Add a value to AppLanguage.
            2. Add a new dictionary entry in BuildStrings() with the same
               keys as the existing languages.
            3. Add it to the LanguageOptions list so it shows up in the
               language picker.
    */
    public static class Localization
    {
        public static event Action? LanguageChanged;

        public static AppLanguage Current { get; private set; } = AppLanguage.PtBr;

        public static readonly (AppLanguage Lang, string DisplayName)[] LanguageOptions = new[]
        {
            (AppLanguage.PtBr, "PT-BR"),
            (AppLanguage.EnUs, "EN-US"),
        };

        static readonly Dictionary<AppLanguage, Dictionary<string, string>> Strings = BuildStrings();

        public static void SetLanguage(AppLanguage language)
        {
            if (Current == language)
                return;

            Current = language;
            LanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (Strings.TryGetValue(Current, out var table) && table.TryGetValue(key, out var value))
                return value;

            // Fallback: try EnUs, then the raw key so missing strings are
            // obvious in the UI instead of crashing.
            if (Strings.TryGetValue(AppLanguage.EnUs, out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
                return fallbackValue;

            return key;
        }

        static Dictionary<AppLanguage, Dictionary<string, string>> BuildStrings()
        {
            var ptBr = new Dictionary<string, string>()
            {
                ["appTitle"] = "Monster Hunter Text Tool",

                ["game3GName"] = "MH 3G / 3U",
                ["game3GFormat"] = "formato GMD",
                ["game4GName"] = "MH 4G / 4U",
                ["game4GFormat"] = "formato LMD",
                ["gameXXName"] = "MHXX / GU",
                ["gameXXFormat"] = "formato GMD",

                ["backToMenu"] = "← Voltar ao menu",
                ["actionsHeader"] = "Ações",
                ["export"] = "Exportar → TXT",
                ["importGmd"] = "Importar → GMD",
                ["importLmd"] = "Importar → LMD",
                ["verify"] = "Verificar TXT",
                ["openGameFolder"] = "Abrir pasta do jogo",

                ["statTotal"] = "Total",
                ["statSuccess"] = "Sucesso",
                ["statFailed"] = "Falhas",
                ["statSkipped"] = "Ignorados",
                ["statFilesUnit"] = "arquivos",

                ["windowTitle3G"] = "Ferramenta GMD - Monster Hunter 3G / 3U",
                ["windowTitle4G"] = "Ferramenta LMD - Monster Hunter 4G / 4U",
                ["windowTitleXX"] = "Ferramenta GMD - Monster Hunter XX / GU",

                ["gameDesc3G"] = "Monster Hunter 3G / 3U - GMD",
                ["gameDesc4G"] = "Monster Hunter 4G / 4U - LMD",
                ["gameDescXX"] = "Monster Hunter XX / GU - GMD",
                ["gameDescNone"] = "Nenhum jogo selecionado",

                ["logReady"] = "Pronto.",
                ["logGame"] = "Jogo: ",
                ["logParser"] = "Parser: ",
                ["logTaskExport"] = "exportado",
                ["logTaskImport"] = "importado",
                ["logTaskVerify"] = "verificado",
                ["logProcessing"] = "processando…",
                ["logMissingTxt"] = "TXT não encontrado: ",
                ["logSkippedSuffix"] = "ignorado",
                ["logErrorPrefix"] = "Erro: ",
                ["logNoGameSelected"] = "Nenhum jogo selecionado.",
                ["logNoFilesFound"] = "Nenhum arquivo encontrado em /original com a extensão ",
                ["logFilesFound"] = "Arquivos encontrados: ",
                ["logTaskLabel"] = "Tarefa: ",
                ["logFinished"] = "Concluído.",
                ["logCouldNotOpenFolder"] = "Não foi possível abrir a pasta: ",
                ["logIconNotFound"] = "Ícone embutido não encontrado: ",
                ["logIconLoadFailed"] = "Falha ao carregar o ícone '",

                ["taskNameExport"] = "Export",
                ["taskNameImport"] = "Import",
                ["taskNameVerify"] = "Verify",

                ["languageButton"] = "Idioma",

                ["quests"] = "Missões",
                ["questsFolderLabel"] = "Pasta de missões (vq.arc, gq.arc, gq2.arc)",
                ["questsFolderNotSet"] = "Nenhuma pasta selecionada",
                ["questsBrowse"] = "Procurar…",
                ["questsBrowseDialogTitle"] = "Selecione a pasta com os arquivos .arc de missões",
                ["questsFilterLabel"] = "Filtrar por categoria e dificuldade",
                ["questCatVillage"] = "Vila",
                ["questCatGuildRegular"] = "Guilda - Normal",
                ["questCatGuildArena"] = "Guilda - Arena",
                ["questCatGuildTraining"] = "Guilda - Treino",
                ["questCatGuildSpecial"] = "Guilda - Especial",
                ["questCatProwlerVillage"] = "Gatuno - Vila",
                ["questCatProwlerGuild"] = "Gatuno - Guilda",
                ["questColumnVillage"] = "Vila (vq)",
                ["questColumnHub"] = "Área de Encontro (gq)",
                ["questColumnPub"] = "Bar dos Caçadores (gq2)",
                ["questColumnSpecial"] = "Especial",
                ["questFilterQuests"] = "Missões",
                ["questFilterProwlers"] = "Gatunos",
                ["questFilterSpecial"] = "Especial",
                ["questsGRankLabel"] = "Ranque: G1 a G4",
                ["questsSpecialLevelLabel"] = "Nível: 1 a 18",
                ["questsRangeTo"] = "a",
                ["questsStarsLabel"] = "Estrelas:",
                ["questsExport"] = "Exportar Missões",
                ["questsImport"] = "Importar Missões",
                ["questsNoFolderSelected"] = "Selecione a pasta com os arquivos .arc antes de continuar.",
                ["questsInvalidStarRange"] = "Intervalo inválido: o mínimo não pode superar o máximo em nenhuma coluna.",
            };

            var enUs = new Dictionary<string, string>()
            {
                ["appTitle"] = "Monster Hunter Text Tool",

                ["game3GName"] = "MH 3G / 3U",
                ["game3GFormat"] = "GMD format",
                ["game4GName"] = "MH 4G / 4U",
                ["game4GFormat"] = "LMD format",
                ["gameXXName"] = "MHXX / GU",
                ["gameXXFormat"] = "GMD format",

                ["backToMenu"] = "← Back to menu",
                ["actionsHeader"] = "Actions",
                ["export"] = "Export → TXT",
                ["importGmd"] = "Import → GMD",
                ["importLmd"] = "Import → LMD",
                ["verify"] = "Verify TXT",
                ["openGameFolder"] = "Open game folder",

                ["statTotal"] = "Total",
                ["statSuccess"] = "Success",
                ["statFailed"] = "Failed",
                ["statSkipped"] = "Skipped",
                ["statFilesUnit"] = "files",

                ["windowTitle3G"] = "GMD Tool - Monster Hunter 3G / 3U",
                ["windowTitle4G"] = "LMD Tool - Monster Hunter 4G / 4U",
                ["windowTitleXX"] = "GMD Tool - Monster Hunter XX / GU",

                ["gameDesc3G"] = "Monster Hunter 3G / 3U - GMD",
                ["gameDesc4G"] = "Monster Hunter 4G / 4U - LMD",
                ["gameDescXX"] = "Monster Hunter XX / GU - GMD",
                ["gameDescNone"] = "No game selected",

                ["logReady"] = "Ready.",
                ["logGame"] = "Game: ",
                ["logParser"] = "Parser: ",
                ["logTaskExport"] = "exported",
                ["logTaskImport"] = "imported",
                ["logTaskVerify"] = "verified",
                ["logProcessing"] = "processing…",
                ["logMissingTxt"] = "Missing TXT: ",
                ["logSkippedSuffix"] = "skipped",
                ["logErrorPrefix"] = "Error: ",
                ["logNoGameSelected"] = "No game selected.",
                ["logNoFilesFound"] = "No files found in /original with extension ",
                ["logFilesFound"] = "Files found: ",
                ["logTaskLabel"] = "Task: ",
                ["logFinished"] = "Finished.",
                ["logCouldNotOpenFolder"] = "Could not open folder: ",
                ["logIconNotFound"] = "Embedded icon not found: ",
                ["logIconLoadFailed"] = "Failed to load icon '",

                ["taskNameExport"] = "Export",
                ["taskNameImport"] = "Import",
                ["taskNameVerify"] = "Verify",

                ["languageButton"] = "Language",

                ["quests"] = "Quests",
                ["questsFolderLabel"] = "Quest folder (vq.arc, gq.arc, gq2.arc)",
                ["questsFolderNotSet"] = "No folder selected",
                ["questsBrowse"] = "Browse…",
                ["questsBrowseDialogTitle"] = "Select the folder containing quest .arc files",
                ["questsFilterLabel"] = "Filter by category and difficulty",
                ["questCatVillage"] = "Village",
                ["questCatGuildRegular"] = "Guild - Regular",
                ["questCatGuildArena"] = "Guild - Arena",
                ["questCatGuildTraining"] = "Guild - Training",
                ["questCatGuildSpecial"] = "Guild - Special",
                ["questCatProwlerVillage"] = "Prowler - Village",
                ["questCatProwlerGuild"] = "Prowler - Guild",
                ["questColumnVillage"] = "Village (vq)",
                ["questColumnHub"] = "Hunters’ Hub (gq)",
                ["questColumnPub"] = "Hunters’ Pub (gq2)",
                ["questColumnSpecial"] = "Special",
                ["questFilterQuests"] = "Quests",
                ["questFilterProwlers"] = "Prowlers",
                ["questFilterSpecial"] = "Special",
                ["questsGRankLabel"] = "Rank: G1 to G4",
                ["questsSpecialLevelLabel"] = "Level: 1 to 18",
                ["questsRangeTo"] = "to",
                ["questsStarsLabel"] = "Stars:",
                ["questsExport"] = "Export Quests",
                ["questsImport"] = "Import Quests",
                ["questsNoFolderSelected"] = "Select the folder containing the .arc files before continuing.",
                ["questsInvalidStarRange"] = "Invalid range: the minimum must not exceed the maximum in any column.",
            };

            return new Dictionary<AppLanguage, Dictionary<string, string>>()
            {
                [AppLanguage.PtBr] = ptBr,
                [AppLanguage.EnUs] = enUs,
            };
        }
    }
}