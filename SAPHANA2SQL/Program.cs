using CommandLine;
using CommandLine.Text;
using System.Data;
using System.Text;

namespace SAPHANA2SQL
{
    internal sealed class Program
    {
        public static void Main(string[] args)
        {
            Console.Clear();
            try
            {
                Config conf  = Config.From(args);
                Console.WriteLine("SAPHANA2SQL\nSource Folder {0}\nRules File {1}\nOutput File {2}\n", conf.SourceFolder, conf.RulesFile, conf.OutputFile);
                TextProcessor.DoProcessing(conf);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Green))
                    Console.WriteLine("Done.");
            }
        }
    }

    // # Separated
    // TextToReplace, ReplaceTextWith
    // UNLOAD PRIORITY 5  AUTO MERGE,\n GO
    public class Rule
    {
        public string? TextToReplace;
        public string? ReplaceTextWith;

        public static Rule FromRule(string line)
        {
            string[] values = line.Split('#');
            Rule rule = new Rule();
            rule.TextToReplace = values[0];
            rule.ReplaceTextWith = values[1];
            return rule;
        }
    }

    internal static class TextProcessor
    {

        // Stringbuilder to hold the translated text
        static StringBuilder sb = new StringBuilder();

        // Structure to hold the rules
        static List<Rule> rules = new List<Rule>();

        public static void DoProcessing(Config config)
        {
            // Open and load the Rules File
            if (System.IO.File.Exists(config.RulesFile))
            {
                try
                {
                    // Bring Rules into Data Store 
                    rules = File.ReadAllLines(config.RulesFile)
                                               .Select(v => Rule.FromRule(v))
                                               .Where((x) => x.TextToReplace != null && x.ReplaceTextWith != null)
                                               .ToList();
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid rules file", e);
                }
            }
            else
                throw new Exception("Could not find rules file");

            // Traverse directories
            Console.Write("Processing Files\n");
            TraverseTree(config.SourceFolder);

            // Write out results
            Console.WriteLine("\n\nWriting output to {0}\n", config.OutputFile);
            System.IO.File.WriteAllText(config.OutputFile, sb.ToString());
        }

        static void DoFileProcesing(System.IO.FileInfo fi)
        {
            // Add Comment on the Script we are converting
            sb.AppendLine(String.Format("\n/****** Converted Script File {0} Time {1} ******/\n", fi.FullName, DateTime.Now));

            // Open the file to read from.
            using (StreamReader sr = fi.OpenText())
            {
                string? s = "";
                while ((s = sr.ReadLine()) != null)
                {
                    foreach (Rule r in rules)
                    {
                        if (r.TextToReplace != null)
                        {
                            if (r.TextToReplace.StartsWith("%")) // Figures that this is and advance replace rule
                            {
                                //% COMMENT ON  "{0}"."{1}"."{2}" is '{3}'#EXEC sys.sp_addextendedproperty @name=N'{2}',@value=N'{3}',@level0type=N'SCHEMA',@level0name=N'{0}',@level1type=N'TABLE',@level1name=N'{1}',@level2type=N'COLUMN',@level2name=N'{2}'

                                // Break and Extract auxiliar variables
                                int codeScriptStartStringLocation = 0;
                                int codeScriptEndStringLocation = 0;
                                int parameterStartStringLocation = 0;
                                int parameterEndStringLocation = 0;

                                codeScriptStartStringLocation = 1;

                                //Find 1st parameters in template 
                                parameterStartStringLocation = r.TextToReplace.IndexOf("{0}");

                                // Check if rule i setup correctly
                                if (parameterStartStringLocation == -1)
                                    throw new Exception(String.Format("Invalid Rule {0}", r.TextToReplace));

                                // Extract code stub
                                codeScriptEndStringLocation = parameterStartStringLocation - 2;
                                string codestub = r.TextToReplace.Substring(codeScriptStartStringLocation, codeScriptEndStringLocation - codeScriptStartStringLocation);

                                //Test if rule apply to this line we just read from file (it must contain the stub)
                                if (s.Contains(codestub))
                                {
                                    // Create a list of extracted parameters
                                    List<string> listofextractedparameters = new List<string>();

                                    // Change problematic sequences to something that will not cause problems (we will change back later)
                                    s = s.Replace("''", "\uFFFE\uFFFE").Replace("\"\"", "\uFFFF\uFFFF");

                                    // Reset/Reuse auxiliar variables now to search for the parameters on the Script File we are procesing
                                    codeScriptStartStringLocation = codeScriptEndStringLocation = parameterStartStringLocation = parameterEndStringLocation = 0;

                                    // Loop thru parameters in the template to find delimiters
                                    string parameterDelimiter = string.Empty;
                                    int templateParamterStringLocation = -1;
                                    int templateParameterIndex = 0;
                                    while (true)
                                    {
                                        // Find the next parameter location in the template
                                        templateParamterStringLocation = r.TextToReplace.IndexOf(String.Format("{{{0}}}", templateParameterIndex));
                                        
                                        // Break if no more parameters expected
                                        if (templateParamterStringLocation == -1)
                                            break;

                                        // Get the parameter delimiter char: " or '
                                        parameterDelimiter = r.TextToReplace.Substring(templateParamterStringLocation - 1, 1);

                                        // Find parameter in the script we are translating
                                        parameterStartStringLocation = s.IndexOf(parameterDelimiter, codeScriptStartStringLocation);
                                        parameterEndStringLocation = s.IndexOf(parameterDelimiter, parameterStartStringLocation + 1);

                                        // Extract parameter and save to our list, also fix escaped chars
                                        string parameter = s.Substring(parameterStartStringLocation + 1, parameterEndStringLocation - parameterStartStringLocation - 1).Replace("\uFFFE\uFFFE", "''").Replace("\uFFFF\uFFFF", "\"\""); ;
                                        listofextractedparameters.Add(parameter);

                                        // move char pointer
                                        codeScriptStartStringLocation = parameterEndStringLocation + 1;

                                        // Move template parameter index
                                        templateParameterIndex++;
                                    }

                                    // Now do the magic!  
                                    s = String.Format(r.ReplaceTextWith, listofextractedparameters.ToArray());
                                }
                            }
                            else // Simple Replace
                                s = s.Replace(r.TextToReplace, r.ReplaceTextWith);
                        }
                    }

                    // Append adjusted text to final result
                    sb.AppendLine(s);
                    sb.AppendLine("GO");
                }
            }
        }

        static void TraverseTree(string root)
        {
            if (root == null)
                return;

            // Data structure to hold names of subfolders to be
            // examined for files.
            Stack<string> dirs = new Stack<string>(200);

            if (!System.IO.Directory.Exists(root))
            {
                throw new Exception("Invalid directory path");
            }
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                // Process the folder

                // Get Subdirecctories
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }

                // Push the subdirectories onto the stack for traversal.
                foreach (string str in subDirs)
                    dirs.Push(str);

                // Get the files
                string[] files = null;
                try
                {
                    // Get Only SQL files
                    files = System.IO.Directory.GetFiles(currentDir, "*.sql");
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }

                // Perform the required action on each file here.
                foreach (string file in files)
                {
                    try
                    {
                        // Process the File
                        DoFileProcesing(new System.IO.FileInfo(file));

                        // Print a progress indicator
                        Console.Write(".");

                    }
                    catch (System.IO.FileNotFoundException e)
                    {
                        // If file was deleted by a separate application
                        //  or thread since the call to TraverseTree()
                        // then just continue.
                        Console.WriteLine(e.Message);
                        continue;
                    }
                }
            }
        }


    }

    internal class Config
    {
        [Option("sourcefolder", Required = true, HelpText = "Path to root of folder with script files to translate")]
        public string? SourceFolder { get; set; }

        [Option("rules", Required = true, HelpText = "Path to text replacement-rules csv file")]
        public string? RulesFile { get; set; }

        [Option("outputfile", Required = true, HelpText = "Path to output file")]
        public string? OutputFile { get; set; }

        internal static Config From(string[] args)
        {
            Config? options = null;

            // Parse command parameters
            var parserResult = (new CommandLine.Parser((settings) =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = null;
            })).ParseArguments<Config>(args);
            parserResult
              .WithParsed<Config>(e => options = e)
              .WithNotParsed(errs => DisplayHelp(parserResult, errs));

            // If parameters are wrong, exit program
            if (options == null)
                Environment.Exit(1);

            return options;
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            HelpText? helpText = null;
            if (errs.IsVersion())
                helpText = HelpText.AutoBuild(result);
            else
            {
                helpText = HelpText.AutoBuild(result, h =>
                {
                    h.AdditionalNewLineAfterOption = false;
                    h.Heading = "SAPHANA2SQL";
                    h.Copyright = @"https://github.com/jmneto";
                    h.AutoVersion = false;
                    return HelpText.DefaultParsingErrorsHandler(result, h);
                }, e => e);
            }
            Console.WriteLine(helpText);
        }
    }

    internal class ConsoleColorContext : IDisposable
    {
        ConsoleColor beforeContextForegroundColor;
        ConsoleColor beforeContextBackgroundColor;


        public ConsoleColorContext(ConsoleColor fgcolor)
        {
            this.beforeContextForegroundColor = Console.ForegroundColor;
            this.beforeContextBackgroundColor = Console.BackgroundColor;
            Console.ForegroundColor = fgcolor;
        }

        public ConsoleColorContext(ConsoleColor fgcolor, ConsoleColor bgcolor)
        {
            this.beforeContextForegroundColor = Console.ForegroundColor;
            this.beforeContextBackgroundColor = Console.BackgroundColor;
            Console.ForegroundColor = fgcolor;
            Console.BackgroundColor = bgcolor;
        }

        public void Dispose()
        {
            Console.ForegroundColor = this.beforeContextForegroundColor;
            Console.BackgroundColor = this.beforeContextBackgroundColor;
        }
    }
}


