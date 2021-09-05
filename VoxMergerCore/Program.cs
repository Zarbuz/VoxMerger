﻿using NDesk.Options;
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
                {"i|input=", "input folder", v => inputFolder = v},
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


        private static void ProcessFolder(string inputFolder, string outputFile, bool logs, bool debug)
        {
            string folder = Path.GetFullPath(inputFolder);

            List<string> files = Directory.GetFiles(folder).ToList();

            List<VoxModel> models = new List<VoxModel>();
            VoxReader reader = new VoxReader();

            Console.WriteLine("[LOG] Started to read all vox files at path: " + folder);
            foreach (string file in files)
            {
                if (Path.GetExtension(file) == ".vox")
                {
                    Console.WriteLine("[LOG] Started to load model: " + file);
                    models.Add(reader.LoadModel(file));
                }

            }

            VoxWriterCustom writer = new VoxWriterCustom();
            if (outputFile.Contains(".vox"))
            {
                writer.WriteModel(outputFile, models);
                reader.LoadModel(outputFile, logs, debug);
            }
            else
            {
                writer.WriteModel(outputFile + ".vox", models);
                reader.LoadModel(outputFile + ".vox", logs, debug);
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
