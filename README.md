# LMD & GMD Editor – Monster Hunter 3 Ultimate / 4 Ultimate Text Tool

LMDTool is a C# utility designed to extract, edit, and rebuild **.lmd and .gmd** text files,  
with full support for multi-line dialogs and in-game formatting such as red text.

This tool was created specifically for:

- **Monster Hunter 4, 4 Ultimate, 4G** → `.lmd`  
- **Monster Hunter 3, 3 Ultimate, 3G** → `.gmd`

Support for other games is not available (yet).



## FEATURES

- Automatic detection of string tables  
- Batch processing of all `.lmd` or `.gmd` files in a folder  
- Export to numbered `.txt` files  
- Import translated text safely  
- Multi-line dialog support  
- Red text formatting support using `<RED>...</RED>`  
- Preserves original structure and offsets  
- Verification mode before rebuilding  
- Handles internal control codes correctly  
- Separate workflows for MH3 and MH4 formats  



## PROJECT STRUCTURE

```

LMDEditor/
│
├─ LMDParser.cs        -> Monster Hunter 4 (LMD) parser
├─ GMDParser.cs        -> Monster Hunter 3 (GMD) parser
├─ Program.cs         -> Program entry point
├─ MainForm.cs        -> GUI
├─ README.txt
└─ /bin
└─ /Release

````



## REQUIREMENTS

- Windows  
- .NET SDK 8.0 or higher  

**Download:**  
https://dotnet.microsoft.com/download

**Check installation:**  
```
dotnet --version
````



## HOW TO BUILD

Open a terminal inside the project folder and run:

```
dotnet publish -c Release -r win-x64 --self-contained true -o build
```

The executable will be generated in:
`bin/Release/net8.0-windows/`



## HOW TO USE

When opening the program, choose the game:

* **MH 3G / 3U** → works with `.gmd`
* **MH 4G / 4U** → works with `.lmd`

The tool will automatically create a folder structure:

```
MH3G/
  ├─ original
  ├─ txt
  ├─ output
  ├─ backup
  └─ logs

MH4G/
  ├─ original
  ├─ txt
  ├─ output
  ├─ backup
  └─ logs
```

Place your original game files inside the correct `original` folder.


## EXPORT TEXT

* Click **Export → TXT**
* All `.lmd` or `.gmd` files will be dumped to `.txt`
* Each string will be numbered



## IMPORT TEXT

* Edit the `.txt` files
* Click **Import → BIN**
* New rebuilt files will appear in the `output` folder
* Originals are backed up automatically



## VERIFY FILES

* Click **Verify TXT**
* Checks if the number and order of strings match
* Prevents broken rebuilds



## TXT FORMAT

Each string starts with an index:

```
[0000] First dialog line
still the same dialog

[0001] Another string
```

**Rules:**

* Line breaks are made using ENTER
* Do not remove the [0000] indices
* Do not change the order
* Do not merge different strings
* Do not create new indices manually
  


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

The tool automatically converts this into Monster Hunter internal color opcodes.



## IMPORTANT RULES FOR TRANSLATORS

* Do NOT delete string numbers
* Do NOT change string order
* Do NOT merge separate entries
* Do NOT remove `<RED>` or `</RED>`
* Do NOT create new [000X] blocks



## TECHNICAL NOTES

* LMD uses UTF-16
* GMD uses mixed binary tables + null-terminated text
* `00 00` is NOT always end of string
* Some internal opcodes exist between characters
* Red text is controlled by embedded binary commands
* The parser dynamically detects real string boundaries
* Verification normalizes internal control codes

