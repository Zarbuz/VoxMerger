using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NDesk.Options;
using VoxMerger.Vox;

namespace VoxMerger
{
    class Program
    {
        static void Main(string[] args)
        {
            string folder = Path.GetFullPath("vox");

            List<string> ext = new List<string> { "vox" };
            List<string> files = Directory.GetFiles(folder).ToList();

            List<VoxModel> models = new List<VoxModel>();
            VoxReader reader = new VoxReader();

            Console.WriteLine("[LOG] Started to read all vox files at path: " + folder);
            foreach (string file in files)
            {
                Console.WriteLine("[LOG] Started to load model: " + file);
                models.Add(reader.LoadModel(file));
            }

            VoxWriterCustom writer = new VoxWriterCustom();
            writer.WriteModel("test.vox", models);

            reader.LoadModel("test.vox");
        }
    }
}
