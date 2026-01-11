# LMD Editor - Monster Hunter 4 Ultimate Text Translator

LMDTool is a C# utility designed to extract, edit, and rebuild .lmd text files,  
with full support for multi-line dialogs and in-game formatting such as red text.

This tool was created for the game Monster Hunter 4, 4 Ultimate and 4G  
specifically, support is not available for other games (yet).

---

## FEATURES

- Automatic detection of string tables  
- Batch processing of all .lmd files in a folder  
- Export to numbered .txt files  
- Import translated text safely  
- Multi-line dialog support  
- Red text formatting support using `<RED>...</RED>`  
- Preserves original structure and offsets  
- Verification mode before rebuilding  
- Handles internal control codes correctly  

---

## PROJECT STRUCTURE

```
LMDEditor/
│
├─ LMDParser.cs        -> Core parser
├─ Program.cs         -> Command-line entry point
├─ MainForm.cs        -> (optional GUI)
├─ README.txt
└─ /bin
   └─ /Release
```

---

## REQUIREMENTS

- Windows  
- .NET SDK 8.0 or higher  

**Download:**  
[https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

**Check installation:**  
```bash
dotnet --version
```

---

## HOW TO BUILD

Open a terminal inside the project folder and run:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o build
```

The executable will be generated in:  
`bin/Release/net8.0-windows/`

---

## HOW TO USE

Place the .exe in the same folder as your .lmd files.

### EXPORT TEXT

```
LMDTool.exe export
```

- All .lmd files will be dumped to .txt files
- Each string will be numbered

### IMPORT TEXT

```
LMDTool.exe import
```

- Reads edited .txt files
- Rebuilds new .lmd files automatically

### VERIFY FILES

```
LMDTool.exe verify
```

- Checks if the number of strings matches
- Prevents broken rebuilds

---

## TXT FORMAT

Each string starts with an index:

```
 First dialog line
still the same dialog

 Another string
```

**Rules:**
- Line breaks are made using ENTER
- Do not remove the [0000] indices
- Do not change the order
- Do not merge different strings
- Do not create new indices manually

---

## RED TEXT SUPPORT

Use tags to mark colored text:

```
<RED>Red text here</RED>
```

**Example:**

```
 You must obtain a
<RED>rare material</RED>
to continue.
```

The tool automatically converts this into Monster Hunter 4 Ultimate internal opcode.

---

## IMPORTANT RULES FOR TRANSLATORS

- Do NOT delete string numbers  
- Do NOT change string order  
- Do NOT merge separate entries  
- Do NOT remove `<RED>` or `</RED>`  
- Do NOT create new [000X] blocks  

---

## TECHNICAL NOTES

- LMD files use UTF-16 encoding  
- 00 00 is NOT always end of string  
- Some internal opcodes exist between characters  
- Red text is controlled by embedded binary commands  
- The parser dynamically detects real string boundaries
