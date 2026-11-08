# LMDEditor — Monster Hunter Text Tool

Windows desktop tool for exporting game text to editable TXT files and rebuilding supported Monster Hunter text assets. Built with C# and Windows Forms on .NET 8.

**Interface languages:** English (EN-US) and Brazilian Portuguese (PT-BR).

[English](#english) · [Português brasileiro](#português-brasileiro)

## English

### Supported workflows

| Game selection | Files | Workflow |
| --- | --- | --- |
| MH 3G / 3U | `.gmd`, supported `.arc` archives | Export, verify and import text; rebuild ARC contents |
| MH 4G / 4U | `.lmd` | Export, verify and import text |
| MH4U — Quests | `quest*.arc`, internal type `1BBFD18E` | Export original quests and language TXT files; import translated text into rebuilt ARCs |
| MHXX / GU | `.gmd` | Dedicated MHXX GMD parser; export, verify and import |
| MHXX / GU — Quests | `vq.arc`, `gq.arc`, `gq2.arc` and matching individual quest ARCs | Export grouped quest texts and apply translations to both grouped and individual archives |

These are implemented workflows, not a guarantee of compatibility with every regional release, platform or archive variant. Recent parser and quest changes still require compilation and in-game validation. Selecting MHXX / GU does not imply that every Switch asset variant is supported.

### Build and run

Build on Windows with the .NET 8 SDK installed. Open a terminal in the folder containing `LMDFileEditor.csproj`:

```powershell
dotnet build .\LMDFileEditor.csproj -c Release
```

Publish a self-contained Windows x64 executable:

```powershell
dotnet publish .\LMDFileEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\build
```

The executable is written to `build/LMDFileEditor.exe`. This self-contained build does not require a separate .NET runtime installation. Run it from a writable location.

The application uses a graphical interface. It does **not** implement `export`, `import` or `verify` command-line arguments.

### Export, translate and import

1. Launch the application and choose a game. Select **EN-US** in the language menu if needed.
2. Click **Open game folder** and place source files in `original/`. Subfolders are searched recursively.
3. Click **Export → TXT** and edit the generated files in `txt/`.
4. Click **Verify TXT** to check the translated files against their originals.
5. Click **Import → GMD** or **Import → LMD**. Rebuilt files are written to `output/`.
6. Test the rebuilt files in your game setup before distributing a translation.

Each game has a separate working directory: `MH3G/`, `MH4G/` or `MHXX/`. Within it, the application creates `original/`, `txt/`, `output/`, `backup/` and `logs/`. These game directories are relative to the application's working directory; use **Open game folder** to locate them.

For MH3U, supported ARC archives can also be placed in `original/`. Their internal GMD texts are exported into a corresponding TXT directory hierarchy. Import rebuilds the archive while preserving entries without matching translated TXT files. MH3U ARC export keeps existing TXT files.

### MHXX / GU quest workflow

Open **Quests**, browse to the folder containing the quest ARCs, and choose the categories and ranges to process. Subfolders are included.

| Filter column | Grouped archive | Export folder | Range |
| --- | --- | --- | --- |
| Village | `vq.arc` | `Village(vq)` | 1–10 stars |
| Hunters’ Hub | `gq.arc` | `Hub(gq)` | 1–10 stars |
| Hunters’ Pub | `gq2.arc` | `Pub(gq2)` | G1–G4 |
| Special | Matching entries in the grouped archives | `Especial/` inside the corresponding archive folder | 1–18 |

- Village, Hub and Pub have separate **Quests** and **Prowlers** checkboxes and range selectors.
- Special has its own checkbox and range. It recognizes subtype `004` names such as `questData_0040101_jpn.gmd` and `questData_0041001_jpn.gmd`.
- Pub currently maps encoded levels 11–14 to folder names G1–G4; this mapping still needs confirmation with additional game samples.
- **Export Quests** exports texts only from the three grouped archives. Existing TXT translations are kept.
- Texts are stored in `MHXX/QuestsText/` next to the executable. Subfolders use `Normal`, `Gatunos` and `Especial`. These folder names stay the same when changing the UI language.
- Edit those TXT files, then click **Import Quests**. The same translation is applied to matching individual quest archives using the internal GMD filename; no second TXT translation is needed.
- Rebuilt archives are written to `MHXX/QuestsOutput/` next to the executable, preserving relative source paths.
- Filtering limits which entries are decompressed and processed. Directory discovery and archive table reading can still be necessary.

Keep the source archives unchanged between export and import. A `source.sha256` marker associates each export with its source; preserve that file. Missing TXT files leave the corresponding entries unchanged.

Duplicate internal paths are handled by archive entry index. Export names may include `__entryNNNN` to distinguish them. If duplicate entries have conflicting translations, propagation to an individual quest archive is blocked and logged.

**Imports are not cumulative:** each run rebuilds from the source archive. For the final output, select all categories and ranges whose translations you want included in the same archive.

### MH4U quests (v005)

Select **MH 4G / 4U → Quests**, choose the folder containing the quest archives, and select the game languages to export or import. English is selected by default. Language selection is independent of the interface language.

The workflow recursively discovers `quest*.arc` with numeric suffixes (for example `quest00.arc` and `quest01.arc`) and processes internal type `0x1BBFD18E`. It accepts the observed ARC version `0x13` and quest layout `0xA0 / v005`.

Files are organized next to the executable:

| Path example | Contents |
| --- | --- |
| `MH4G/QuestsText/quest00/source.sha256` | Source archive identity |
| `MH4G/QuestsText/quest00/loc/quest/m00101.1BBFD18E` | Extracted original quest for inspection |
| `MH4G/QuestsText/quest00.arc__loc~quest~m00101__0001__eng.txt` | Seven English text fields to translate |
| `MH4G/QuestsText/quest00.arc__loc~quest~m00101__0001__fre.txt` | French fields, when selected |
| `MH4G/QuestsOutput/quest00.arc` | Rebuilt archive |

Other supported language slots are `spa`, `ger` and `ita`. Source subfolders and internal ARC paths are preserved. Duplicate internal names receive an entry-index suffix. Classification by Village/Hub/stars is not implemented for MH4U: the provided samples do not yet establish a reliable mapping.

The TXT format has seven indexed lines, `[0000]` through `[0006]`, plus a required format header. Their observed contents are title, main objective, failure condition, description, monster information, client and subquest. In this format, use `\n` for a line break, `\r` for carriage return, `\t` for a tab and `\\` for a literal backslash. Keep all indices and the header. These rules apply to the new MH4U quest parser, not to the existing MH3U or LMD formats.

Export preserves existing TXT files. Import reads TXT files, **not manual edits to the extracted `.1BBFD18E` copies**. Missing or unselected language files preserve original text. An unchanged export/import preserves the original quest and ARC bytes. Changed strings are appended and only their known pointers are updated; existing gameplay sections are not moved. The rebuilt ARC is checked before it is written.

Use unchanged source ARCs for every import and retain `source.sha256`. Imports start from the source, so select all translated languages needed in the final output. An invalid quest or TXT aborts rebuilding that archive, and the error is logged. The other archives may still be processed.

C# tests passed for all **207 entries of the supplied quest00.arc** and one separate quest: unchanged round trips, longer Unicode translations, preservation of unrelated bytes and batch reinsertion. The full Windows Forms build and visual validation remain pending. Binary analysis also covered these samples. The second real ARC and in-game behavior have not been tested. Structural validation cannot guarantee game compatibility.

### Editing TXT files

Keep the identifiers generated by the parser, their order, and the boundaries between entries. Depending on the format, identifiers may be numeric or named.

```text
[0000] First line of a message.
Continuation of the same message.
[0001] Another message.
```

This illustrates entry structure; preserve the exact separators emitted by your parser. Do not add, remove or merge entries. Keep placeholders such as `%s` and `%d`, and preserve game formatting tags.

- **MH4 LMD:** supports multiline text and `<RED>...</RED>` markup.
- **MH3U GMD:** current exports use actual line breaks, tabs and backslashes instead of an escaped-text header. Older exports with the recognized v2 header remain readable. Do not manually turn control characters into literal `\r`, `\n` or `\t` sequences.
- **MHXX GMD:** uses its own TXT conversion rules. Preserve its generated escaping and tags such as `<COL FF0000FF>...</COL>`; MH3U's plain-text behavior does not apply to this parser.

Verification checks structural consistency; it does not guarantee that translated text fits the game's text boxes or that a rebuilt asset works in-game.

### Implementation notes

- Separate parsers handle LMD, MH3U GMD and MHXX GMD.
- ARC rebuilding preserves unrelated entries and retains archive metadata where supported.
- Nameless MHXX GMDs locate their text pool immediately after the internal filename. A stale positive text-pool size can be tolerated only if the parser's string-count, UTF-8 and terminator checks pass; import recalculates the text-pool size.
- This is an extraction and reinsertion tool for manual translation, not an automatic translation service.

### Source files

| File | Purpose |
| --- | --- |
| `Program.cs` | Windows Forms entry point |
| `MainForm.cs` | Game selection, language menu and processing interface |
| `Localization.cs` | PT-BR and EN-US interface strings |
| `Appconfig.cs` | Application configuration |
| `LMDParser.cs` | LMD text conversion |
| `GMDParser.cs` | MH3U GMD text conversion |
| `MHXXGMDParser.cs` | MHXX GMD text conversion |
| `Arcparser.cs` | ARC reading and rebuilding |
| `MH3UArcWorkflow.cs` | MH3U ARC text workflow |
| `Questbatch.cs` | Grouped quest processing and individual archive synchronization |
| `Questnaming.cs` | Quest filename classification and filtering |
| `MH4QuestParser.cs` | MH4U v005 text pointers, UTF-16 and TXT conversion |
| `MH4QuestBatch.cs` | MH4U quest ARC export/import and source identity checks |

## Português brasileiro

### Sobre o programa

O LMDEditor exporta textos para tradução manual e reinsere os TXT nos formatos implementados: GMD do MH3G/3U, LMD do MH4G/4U e GMD do MHXX/GU. Também oferece processamento de ARC do MH3U e um fluxo específico para missões do MHXX/GU.

A interface pode ser alternada entre **PT-BR** e **EN-US**. As funções implementadas não garantem compatibilidade com todas as regiões, plataformas ou variantes dos arquivos. As correções recentes ainda precisam de compilação e validação dentro do jogo.

### Compilar

No Windows, com o SDK .NET 8 instalado, abra o terminal na pasta do projeto:

```powershell
dotnet build .\LMDFileEditor.csproj -c Release
```

Para gerar um executável independente para Windows x64:

```powershell
dotnet publish .\LMDFileEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\build
```

O resultado fica em `build/LMDFileEditor.exe`. Essa publicação inclui o runtime. Execute em uma pasta com permissão de escrita. O programa é gráfico: os antigos comandos `export`, `import` e `verify` pelo terminal não são implementados.

### Uso básico

1. Abra o programa, selecione o jogo e use **Abrir pasta do jogo**.
2. Coloque os arquivos em `original/`; a busca inclui subpastas.
3. Clique em **Exportar → TXT** e traduza os arquivos em `txt/`.
4. Use **Verificar TXT** antes de reinserir.
5. Clique em **Importar → GMD** ou **Importar → LMD** e recolha os arquivos em `output/`.
6. Teste o resultado no jogo.

Os diretórios de trabalho são `MH3G/`, `MH4G/` e `MHXX/`, relativos à pasta de trabalho do aplicativo. Dentro deles são criadas as pastas `original/`, `txt/`, `output/`, `backup/` e `logs/`.

No MH3U, arquivos ARC compatíveis também podem ser colocados em `original/`. A exportação organiza os textos dos GMD internos em subpastas e mantém TXT existentes. A importação preserva entradas sem TXT correspondente.

### Missões do MHXX / GU

Na tela **Missões**, escolha a pasta de origem dos ARC e configure os filtros:

| Coluna | Arquivo | Pasta de extração | Intervalo |
| --- | --- | --- | --- |
| Vila | `vq.arc` | `Village(vq)` | 1–10 estrelas |
| Área de Encontro | `gq.arc` | `Hub(gq)` | 1–10 estrelas |
| Bar dos Caçadores | `gq2.arc` | `Pub(gq2)` | G1–G4 |
| Especial | Entradas correspondentes nos arquivos agrupados | `Especial/`, dentro da pasta do arquivo | 1–18 |

Vila, Área de Encontro e Bar dos Caçadores têm opções de **Missões** e **Gatunos**, com intervalos independentes. **Especial** possui seu próprio filtro para nomes do subtipo `004`, como `questData_0040101_jpn.gmd`. O mapeamento atual do Bar usa níveis codificados de 11 a 14 para G1 a G4 e ainda requer confirmação com mais amostras.

**Exportar Missões** extrai textos somente dos três ARC agrupados para `MHXX/QuestsText/`, ao lado do executável. Os TXT existentes são mantidos. As subpastas `Normal`, `Gatunos` e `Especial` não mudam com o idioma da interface.

Após traduzir, **Importar Missões** aplica os textos aos arquivos agrupados e aos ARC individuais correspondentes, usando o nome interno do GMD. O resultado fica em `MHXX/QuestsOutput/`, preservando os caminhos relativos. Os filtros reduzem o processamento das entradas; a descoberta de arquivos e a leitura de tabelas ainda podem ser necessárias.

- Mantenha os ARC de origem e os arquivos `source.sha256` associados à exportação.
- TXT ausente mantém a entrada original.
- Caminhos internos duplicados podem gerar nomes com `__entryNNNN`. Traduções conflitantes impedem a propagação para a missão individual e geram uma mensagem no log.
- **As importações não são cumulativas:** cada execução reconstrói a partir do original. Na importação final, marque todas as categorias e intervalos que devem entrar no mesmo ARC.

### Missões do MH4U

Selecione **MH 4G / 4U → Missões**, escolha a pasta com os dois ARC e marque os idiomas do jogo. Inglês vem marcado inicialmente. A busca recursiva aceita nomes `quest` seguidos de números e `.arc`, como `quest00.arc` e `quest01.arc`.

A extração fica em `MH4G/QuestsText`, ao lado do executável. Exemplo:

- `quest00/source.sha256`: identificação do ARC original.
- `quest00/loc/quest/m00101.1BBFD18E`: cópia original da missão.
- `quest00.arc__loc~quest~m00101__0001__eng.txt`: sete campos de texto em inglês.
- `quest00.arc__loc~quest~m00101__0001__fre.txt`: francês, quando selecionado. Também há `spa`, `ger` e `ita`.

O ARC reconstruído fica em `MH4G/QuestsOutput/quest00.arc`. Os caminhos internos e as subpastas da origem são preservados. A organização por Vila/Área de Encontro/estrelas ainda não foi implementada para o MH4U: falta confirmar essa classificação.

Traduza os TXT mantendo o cabeçalho e os índices `[0000]` a `[0006]`. Neste formato específico, cada entrada ocupa uma linha no editor: escreva `\n` para quebra de linha, `\r` para retorno de carro, `\t` para tabulação e `\\` para uma barra literal. Não altere esses escapes para sequências duplicadas. O parser converte os escapes em caracteres reais ao reinserir.

A exportação mantém TXT existentes. A importação usa esses TXT; alterações manuais nas cópias `.1BBFD18E` não são importadas. Textos ausentes ou idiomas desmarcados preservam o original. Os textos alterados são acrescentados ao final da missão e seus ponteiros são atualizados, sem deslocar os dados de gameplay.

Mantenha os ARC originais e `source.sha256`. Na importação final, marque todos os idiomas traduzidos, pois cada execução parte da origem. Um erro interrompe a reconstrução daquele ARC e aparece no log; outros ARC podem continuar sendo processados.

Os testes em C# passaram nas **207 missões do quest00.arc fornecido e em uma missão avulsa**, incluindo ida e volta sem alterações, textos Unicode maiores e preservação dos demais bytes. A compilação completa da interface e a validação visual ainda estão pendentes. O segundo ARC real e o funcionamento no jogo ainda precisam de teste. O suporte inicial aceita a versão de ARC `0x13` e o cabeçalho de missão `0xA0 / v005`.

### Regras de tradução

Preserve os identificadores exportados, a ordem das entradas e os separadores. Não acrescente, apague ou una entradas. Mantenha placeholders como `%s` e `%d` e as tags de formatação.

LMD do MH4 aceita texto multilinha e `<RED>...</RED>`. A exportação atual do MH3U usa quebras reais, tabulações e barras, sem o cabeçalho de escapes; o formato antigo com cabeçalho v2 reconhecido continua legível. O MHXX tem regras próprias de escape e tags como `<COL FF0000FF>...</COL>`.

A verificação estrutural não garante que o texto caiba nas caixas do jogo. Preserve uma cópia dos originais e valide a tradução no jogo.

## License / Licença

MIT — see [LICENSE.txt](LICENSE.txt) / consulte [LICENSE.txt](LICENSE.txt).

### Folder migration / Migração de pastas

Quest operations automatically move legacy `QuestsText` and `QuestsOutput` from the executable folder into `MHXX/`, and from `MH4U/` into `MH4G/`. Existing different versions block migration with a message showing the conflicting paths; nothing is silently overwritten. Keep the updated executable in the same publish folder to find the old folders.

All newly exported MH4U TXT files share `MH4G/QuestsText/`. Names include the source ARC relative path, internal quest path, entry index (zero-based), and language. Example: `quest00.arc__loc~quest~m00101__0001__eng.txt` (the index depends on the ARC). `~` represents a path separator; special characters are encoded as `%XX`. Do not rename these files. Original quest copies and `source.sha256` remain in archive subfolders. When a selected language is exported or imported, its old nested TXT is moved to its flat name without replacing translations. MHXX retains its category and star subfolders.

Ao exportar ou importar missões, o programa move as antigas pastas `QuestsText` e `QuestsOutput` da raiz para `MHXX/`, e as de `MH4U/` para `MH4G/`. Mantenha o executável atualizado na mesma pasta publish. Arquivos diferentes com o mesmo destino interrompem a migração com os caminhos no log; nenhuma versão é escolhida automaticamente.

Os TXT novos do MH4U ficam todos diretamente em `MH4G/QuestsText/`. O nome contém ARC, caminho interno da missão, índice da entrada (começando em zero) e idioma. Exemplo: `quest00.arc__loc~quest~m00101__0001__eng.txt` (o índice depende do ARC). `~` representa separação de pastas e caracteres especiais usam `%XX`. Não renomeie os TXT. As cópias originais e `source.sha256` continuam nas subpastas dos ARC. TXT antigos dos idiomas selecionados são movidos durante a exportação ou importação, preservando as traduções. As categorias e estrelas do MHXX permanecem organizadas em subpastas.
