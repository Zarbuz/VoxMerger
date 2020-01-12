﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxMerger.Extensions;
using VoxMerger.Schematics;
using VoxMerger.Schematics.Tools;
using VoxMerger.Utils;
using VoxMerger.Vox.Chunks;

namespace VoxMerger.Vox
{
    public class VoxWriterCustom : VoxParser
    {
        private List<VoxModel> _models;
        private int _totalBlockCount;
        private int _countSize;
        private List<Color> _usedColors = new List<Color>();

        public bool WriteModel(string absolutePath, List<VoxModel> models)
        {
            _models = models;
            _usedColors.Clear();
            using (var writer = new BinaryWriter(File.Open(absolutePath, FileMode.Create)))
            {
                writer.Write(Encoding.UTF8.GetBytes(HEADER));
                writer.Write(VERSION);
                writer.Write(Encoding.UTF8.GetBytes(MAIN));
                writer.Write(0);
                writer.Write(CountChildrenSize());
                WriteChunks(writer);
            }

            return true;
        }

        private int CountChildrenSize()
        {
            int childrenChunkSize = 0;
            _countSize = CountTotalRegions();
            _totalBlockCount = CountTotalBlocks();
            Console.WriteLine("[INFO] Total blocks: " + _totalBlockCount);

            int chunkSize = 24 * _countSize; //24 = 12 bytes for header and 12 bytes of content
            int chunkXYZI = (16 * _countSize) + _totalBlockCount * 4; //16 = 12 bytes for header and 4 for the voxel count + (number of voxels) * 4
            int chunknTRNMain = 40; //40 = 
            int chunknGRP = 24 + _countSize * 4;
            int chunknTRN = 60 * _countSize;
            int chunknSHP = 32 * _countSize;
            int chunkRGBA = 1024 + 12;

            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 0; j < _models[i].voxelFrames.Count; j++)
                {
                    Vector3 worldPosition = _models[i].transformNodeChunks[j + 1].TranslationAt();
                    Rotation rotation = _models[i].transformNodeChunks[j + 1].RotationAt();

                    string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;
                    chunknTRN += Encoding.UTF8.GetByteCount(pos);
                    chunknTRN += Encoding.UTF8.GetByteCount(Convert.ToString((byte)rotation));
                }
            }

            childrenChunkSize = chunkSize; //SIZE CHUNK
            childrenChunkSize += chunkXYZI; //XYZI CHUNK
            childrenChunkSize += chunknTRNMain; //First nTRN CHUNK (constant)
            childrenChunkSize += chunknGRP; //nGRP CHUNK
            childrenChunkSize += chunknTRN; //nTRN CHUNK
            childrenChunkSize += chunknSHP;
            childrenChunkSize += chunkRGBA;

            return childrenChunkSize;
        }

        private int CountTotalBlocks()
        {
            int total = 0;

            for (int i = 0; i < _models.Count; i++)
            {
                Color[] colorsPalette = _models[i].palette;
                for (int j = 0; j < _models[i].voxelFrames.Count; j++)
                {
                    VoxelData data = _models[i].voxelFrames[j];
                    for (int y = 0; y < data.VoxelsTall; y++)
                    {
                        for (int z = 0; z < data.VoxelsDeep; z++)
                        {
                            for (int x = 0; x < data.VoxelsWide; x++)
                            {
                                int indexColor = data.Get(x, y, z);
                                Color color = colorsPalette[indexColor];
                                if (color != Color.Empty)
                                {
                                    total++;
                                }
                            }
                        }
                    }
                }
            }
            return total;
        }

        private int CountTotalRegions()
        {
            return _models.Sum(t => t.voxelFrames.Count);
        }

        /// <summary>
        /// Main loop for write all chunks
        /// </summary>
        /// <param name="writer"></param>
        private void WriteChunks(BinaryWriter writer)
        {
            WritePaletteChunk(writer);
            using (var progressbar = new ProgressBar())
            {
                Console.WriteLine("[LOG] Started to write chunks ...");

                for (int i = 0; i < _models.Count; i++)
                {
                    for (int j = 0; j < _models[i].voxelFrames.Count; j++)
                    {
                        WriteSizeChunk(writer, _models[i].transformNodeChunks[j + 1].TranslationAt());
                        WriteXyziChunk(writer, _models[i], j);
                        float progress = ((float)i / _countSize);
                        progressbar.Report(progress);
                    }
                }
                Console.WriteLine("[LOG] Done.");
            }

            WriteMainTranformNode(writer);
            WriteGroupChunk(writer);

            int index = 0;
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 0; j < _models[i].voxelFrames.Count; j++)
                {
                    WriteTransformChunk(writer, _models[i].transformNodeChunks[j + 1], index);
                    WriteShapeChunk(writer, index);
                    index++;
                }
            }

        }

        /// <summary>
        /// Write nTRN chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private void WriteTransformChunk(BinaryWriter writer, TransformNodeChunk transformNode, int index)
        {
            writer.Write(Encoding.UTF8.GetBytes(nTRN));
            Vector3 worldPosition = transformNode.TranslationAt();
            string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;

            writer.Write(48 + Encoding.UTF8.GetByteCount(pos)
                            + Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt()))); //nTRN chunk size
            writer.Write(0); //nTRN child chunk size
            writer.Write(2 * index + 2); //ID
            writer.Write(0); //ReadDICT size for attributes (none)
            writer.Write(2 * index + 3);//Child ID
            writer.Write(-1); //Reserved ID
            writer.Write(-1); //Layer ID
            writer.Write(1); //Read Array Size
            writer.Write(2); //Read DICT Size (previously 1)

            writer.Write(2); //Read STRING size
            writer.Write(Encoding.UTF8.GetBytes("_r"));
            writer.Write(Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt())));
            writer.Write(Encoding.UTF8.GetBytes(Convert.ToString((byte)transformNode.RotationAt())));


            writer.Write(2); //Read STRING Size
            writer.Write(Encoding.UTF8.GetBytes("_t"));
            writer.Write(Encoding.UTF8.GetByteCount(pos));
            writer.Write(Encoding.UTF8.GetBytes(pos));
        }


        /// <summary>
        /// Write nSHP chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private void WriteShapeChunk(BinaryWriter writer, int index)
        {
            writer.Write(Encoding.UTF8.GetBytes(nSHP));
            writer.Write(20); //nSHP chunk size
            writer.Write(0); //nSHP child chunk size
            writer.Write(2 * index + 3); //ID
            writer.Write(0);
            writer.Write(1);
            writer.Write(index);
            writer.Write(0);
        }


        /// <summary>
        /// Write the main trande node chunk
        /// </summary>
        /// <param name="writer"></param>
        private void WriteMainTranformNode(BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes(nTRN));
            writer.Write(28); //Main nTRN has always a 28 bytes size
            writer.Write(0); //Child nTRN chunk size
            writer.Write(0); // ID of nTRN
            writer.Write(0); //ReadDICT size for attributes (none)
            writer.Write(1); //Child ID
            writer.Write(-1); //Reserved ID
            writer.Write(-1); //Layer ID
            writer.Write(1); //Read Array Size
            writer.Write(0); //ReadDICT size
        }

        /// <summary>
        /// Write nGRP chunk
        /// </summary>
        /// <param name="writer"></param>
        private void WriteGroupChunk(BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes(nGRP));
            writer.Write(16 + (4 * (_countSize - 1))); //nGRP chunk size
            writer.Write(0); //Child nGRP chunk size
            writer.Write(1); //ID of nGRP
            writer.Write(0); //Read DICT size for attributes (none)
            writer.Write(_countSize);
            for (int i = 0; i < _countSize; i++)
            {
                writer.Write((2 * i) + 2); //id for childrens (start at 2, increment by 2)
            }
        }

        /// <summary>
        /// Write SIZE chunk
        /// </summary>
        /// <param name="writer"></param>
        private void WriteSizeChunk(BinaryWriter writer, Vector3 volumeSize)
        {
            writer.Write(Encoding.UTF8.GetBytes(SIZE));
            writer.Write(12); //Chunk Size (constant)
            writer.Write(0); //Child Chunk Size (constant)

            writer.Write(volumeSize.X); //Width
            writer.Write(volumeSize.Y); //Height
            writer.Write(volumeSize.Z); //Depth
        }

        /// <summary>
        /// Write XYZI chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private void WriteXyziChunk(BinaryWriter writer, VoxModel model, int index)
        {
            writer.Write(Encoding.UTF8.GetBytes(XYZI));
            writer.Write((model.voxelFrames[index].Colors.Count(t => t != 0) * 4) + 4); //XYZI chunk size
            writer.Write(0); //Child chunk size (constant)
            writer.Write(model.voxelFrames[index].Colors.Count(t => t != 0)); //Blocks count

            for (int tall = 0; tall < model.voxelFrames[index].VoxelsTall; tall++)
            {
                for (int deep = 0; deep < model.voxelFrames[index].VoxelsDeep; deep++)
                {
                    for (int wide = 0; wide < model.voxelFrames[index].VoxelsWide; wide++)
                    {
                        int paletteIndex = model.voxelFrames[index].Get(wide, tall, deep);
                        Color color = model.palette[paletteIndex];
                        writer.Write(wide);
                        writer.Write(tall);
                        writer.Write(deep);

                        int i = _usedColors.IndexOf(color) + 1;
                        writer.Write((i != 0) ? (byte)i : (byte)1);
                    }
                }
            }
        }

        /// <summary>
        /// Write RGBA chunk
        /// </summary>
        /// <param name="writer"></param>
        private void WritePaletteChunk(BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes(RGBA));
            writer.Write(1024);
            writer.Write(0);
            _usedColors = new List<Color>(256);

            foreach (VoxModel model in _models)
            {
                foreach (Color color in model.palette)
                {
                    if (_usedColors.Count < 256 && !_usedColors.Contains(color) && color != Color.Empty)
                    {
                        _usedColors.Add(color);
                        writer.Write(color.R);
                        writer.Write(color.G);
                        writer.Write(color.B);
                        writer.Write(color.A);
                    }
                }
            }

            for (int i = (256 - _usedColors.Count); i >= 1; i--)
            {
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
            }
        }
    }
}