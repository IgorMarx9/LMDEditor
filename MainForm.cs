using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class MainForm : Form
{
    ProgressBar bar;
    TextBox log;

    public MainForm()
    {
        BuildUI();
    }


    void BuildUI()
    {
        Text = "Extrator de LMD - Dodogama Team";
        Width = 640;
        Height = 480;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        Button export = new Button() { Text = "Extrair LMD → TXT", Left = 20, Top = 20, Width = 200 };
        Button import = new Button() { Text = "Gerar TXT → LMD", Left = 20, Top = 60, Width = 200 };
        Button verify = new Button() { Text = "Verificar TXT", Left = 20, Top = 100, Width = 200 };

        bar = new ProgressBar() { Left = 20, Top = 150, Width = 200, Height = 25 };
        log = new TextBox()
        {
            Left = 20,
            Top = 190,
            Width = 200,
            Height = 230,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        export.Click += (s, e) => RunTask(TaskMode.Export);
        import.Click += (s, e) => RunTask(TaskMode.Import);
        verify.Click += (s, e) => RunTask(TaskMode.Verify);

        Controls.Add(export);
        Controls.Add(import);
        Controls.Add(verify);
        Controls.Add(bar);
        Controls.Add(log);

        Directory.CreateDirectory("original");
        Directory.CreateDirectory("txt");
        Directory.CreateDirectory("output");
        Directory.CreateDirectory("backup");
        Directory.CreateDirectory("logs");
    }

    enum TaskMode { Export, Import, Verify }

    void RunTask(TaskMode mode)
    {
        log.Clear();

        string[] files = Directory.GetFiles("original", "*.lmd");
        if (files.Length == 0)
        {
            MessageBox.Show("Nenhum .lmd encontrado na pasta /original");
            return;
        }

        bar.Value = 0;
        bar.Maximum = files.Length;

        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            Log("Processando: " + name);

            try
            {
                if (mode == TaskMode.Export)
                {
                    string outTxt = Path.Combine("txt", Path.ChangeExtension(name, ".txt"));
                    LMDParser.ExportToTxt(file, outTxt);
                }
                else
                {
                    string txt = Path.Combine("txt", Path.ChangeExtension(name, ".txt"));
                    if (!File.Exists(txt))
                    {
                        Log("⚠ TXT não encontrado, pulando.");
                        continue;
                    }

                    if (mode == TaskMode.Verify)
                    {
                        LMDParser.Verify(file, txt);
                        Log("✔ Verificação OK.");
                    }
                    else
                    {
                        string backup = Path.Combine("backup", name);
                        File.Copy(file, backup, true);

                        string outLmd = Path.Combine("output", name);
                        LMDParser.ImportFromTxt(file, txt, outLmd);
                        Log("✔ Gerado com sucesso.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("❌ ERRO: " + ex.Message);
                File.AppendAllText("logs\\errors.log",
                    $"[{DateTime.Now}] {name}: {ex}\n");
            }

            bar.Value++;
            Application.DoEvents();
        }

        Log("\nConcluído.");
        MessageBox.Show("Processo finalizado.");
    }

    void Log(string msg)
    {
        log.AppendText(msg + Environment.NewLine);
    }
}
