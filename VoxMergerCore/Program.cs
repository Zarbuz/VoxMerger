using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileToVoxCore.Vox;
using VoxMerger.Vox;

namespace VoxMerger
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFolder = string.Empty;
            string outputFile = string.Empty;
            bool shopHelp = false;
            bool debug = false;
            bool logs = false;
            OptionSet options = new OptionSet()
            {
                {"i|input=", "input (list of files or folder)", v => inputFolder = v},
                {"o|output=", "output file", v => outputFile = v},
                {"h|help", "show this message and exit", v => shopHelp = v != null},
                {"d|debug", "enable debug verification", v => debug = v != null },
                {"l|log", "enable writing logs", v =>  logs = v != null}
            };

            List<string> extra = options.Parse(args);
            CheckHelp(options, shopHelp);
            CheckArguments(inputFolder, outputFile);
            ProcessFolder(inputFolder, outputFile, logs, debug);

        }


        private static void ProcessFolder(string input, string outputFile, bool logs, bool debug)
        {
            try
	        {
		        Console.WriteLine("[LOG] Started to read all vox files at path: " + input);
		        string folder = Path.GetFullPath(input);
		        List<string> files = Directory.GetFiles(folder).ToList();
		        ReadVoxelModels(files, outputFile, logs, debug);
	        }
	        catch (Exception e)
	        {
		        List<string> files = input.Split(",").ToList();
		        ReadVoxelModels(files, outputFile, logs, debug);
            }
        }

        private static void ReadVoxelModels(List<string> files, string outputFile, bool logs, bool debug)
		{
			List<VoxModel> models = new List<VoxModel>();
			VoxReader reader = new VoxReader();

            foreach (string file in files.Where(file => Path.GetExtension(file) == ".vox"))
            {
	            Console.WriteLine("[LOG] Started to load model: " + file);
	            models.Add(reader.LoadModel(file, false, false, false, false));
            }

            VoxWriterCustom writer = new VoxWriterCustom();
            outputFile = outputFile.Contains(".vox") ? outputFile : outputFile + ".vox";
            writer.WriteModel(outputFile, models);
            if (debug)
			{
				reader.LoadModel(outputFile, logs, true);
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
