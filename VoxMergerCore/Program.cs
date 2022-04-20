using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileToVoxCore.Vox;
using VoxMerger.Vox;
using VoxMergerCore.DTO;

namespace VoxMerger
{
	internal class Program
    {
	    private static void Main(string[] args)
	    {
		    ProgramOptions programOptions = new ProgramOptions();
            OptionSet options = new OptionSet()
            {
                {"i|input=", "input (list of files or folder)", v => programOptions.InputFolder = v},
                {"o|output=", "output file", v => programOptions.OutputFile = v},
                {"h|help", "show this message and exit", v => programOptions.ShopHelp = v != null},
                {"d|debug", "enable debug verification", v => programOptions.EnableDebug = v != null },
                {"l|log", "enable writing logs", v =>  programOptions.EnableLogs = v != null},
                {"u|union", "union all regions into one", v =>  programOptions.Union = v != null}
            };

            List<string> extra = options.Parse(args);
            CheckHelp(options, programOptions.ShopHelp);
            CheckArguments(programOptions.InputFolder, programOptions.OutputFile);
            ProcessFolder(programOptions);
        }

        private static void ProcessFolder(ProgramOptions programOptions)
        {
	        List<string> files;
	        string folder = Path.GetFullPath(programOptions.InputFolder);
	        if (Directory.Exists(folder))
	        {
		        Console.WriteLine("[LOG] Started to read all vox files at path: " + programOptions.InputFolder);
		        files = Directory.GetFiles(folder).ToList();
            }
	        else
	        {
		        files = programOptions.InputFolder.Split(",").ToList();
            }
            
            ReadVoxelModels(files, programOptions);
        }

        private static void ReadVoxelModels(List<string> files, ProgramOptions programOptions)
		{
			List<VoxModel> models = new List<VoxModel>();
			VoxReader reader = new VoxReader();

            foreach (string file in files.Where(file => Path.GetExtension(file) == ".vox"))
            {
	            Console.WriteLine("[LOG] Started to load model: " + file);
	            models.Add(reader.LoadModel(file, false, false, false));
            }

            VoxWriterCustom writer = new VoxWriterCustom();
            if (models.Count == 0)
            {
                Console.WriteLine("[ERR] No models founds! Abort.");
            }
            else
            {
	            writer.WriteModel(programOptions.GetSageOutputFile(), models);
	            if (programOptions.EnableDebug)
	            {
		            reader.LoadModel(programOptions.OutputFile, programOptions.EnableLogs, true);
	            }
            }
        }

        private static void CheckArguments(string inputFolder, string outputFile)
        {
            if (string.IsNullOrEmpty(inputFolder))
            {
                Console.WriteLine("[ERR] Missing input folder path. Check help for more informations.");
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(outputFile))
            {
                Console.WriteLine("[ERR] Missing output file path. Check help for more informations.");
                Environment.Exit(1);
            }
        }
        private static void CheckHelp(OptionSet options, bool shopHelp)
        {
            if (shopHelp)
            {
                ShowHelp(options);
                Environment.Exit(0);
            }
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: VoxMerge --i INPUT --o OUTPUT");
            Console.WriteLine("Options: ");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
