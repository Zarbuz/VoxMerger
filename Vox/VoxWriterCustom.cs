using FileToVoxCore.Schematics.Tools;
using FileToVoxCore.Vox;
using FileToVoxCore.Vox.Chunks;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using VoxMerger.Utils;

namespace VoxMerger.Vox
{
	public class VoxWriterCustom : VoxParser
    {
        private List<VoxModel> _models;
        private int _totalBlockCount;
        private List<Color> _usedColors = new List<Color>();
        private Dictionary<int, KeyValuePair<int, int>> _usedIndexColors = new Dictionary<int, KeyValuePair<int, int>>();
        public bool WriteModel(string absolutePath, List<VoxModel> models)
        {
            _models = models;
            _usedColors.Clear();
            _usedIndexColors.Clear();
            using (var writer = new BinaryWriter(File.Open(absolutePath, FileMode.Create)))
            {
                writer.Write(Encoding.UTF8.GetBytes(HEADER));
                writer.Write(VERSION);
                writer.Write(Encoding.UTF8.GetBytes(MAIN));
                writer.Write(0);
                int childrenSize = CountChildrenSize();
                writer.Write(childrenSize);
                int byteWritten = WriteChunks(writer);

                Console.WriteLine("[LOG] Bytes to write for childs chunks: " + childrenSize);
                Console.WriteLine("[LOG] Bytes written: " + byteWritten);
                if (byteWritten != childrenSize)
                {
                    Console.WriteLine("[LOG] Children size and bytes written isn't the same! Vox is corrupted!");
                    Console.ReadKey();
                    return false;
                }
            }

            return true;
        }

        private int CountChildrenSize()
        {
            _totalBlockCount = CountTotalBlocks();
            int totalModels = CountTotalModels();
            Console.WriteLine("[INFO] Total blocks: " + _totalBlockCount);

            int chunkSIZE = 24 * totalModels; //24 = 12 bytes for header and 12 bytes of content
            int chunkXYZI = (16 * totalModels) + _totalBlockCount * 4; //16 = 12 bytes for header and 4 for the voxel count + (number of voxels) * 4
            int chunknGRP = CountGroupChunkSize();
            int chunknTRN = CountTransformChunkSize();
            int chunknSHP = CountShapeChunkSize();
            int chunkRGBA = 1024 + 12;
            int chunkMATL = CountMaterialChunksize();
            int chunkMnTRN = 40;
            int chunkMnGRP = CountMainGroupChunkSize();

            Console.WriteLine("[LOG] Chunk RGBA: " + chunkRGBA);
            Console.WriteLine("[LOG] Chunk MATL: " + chunkMATL);
            Console.WriteLine("[LOG] Chunk SIZE: " + chunkSIZE);
            Console.WriteLine("[LOG] Chunk XYZI: " + chunkXYZI);
            Console.WriteLine("[LOG] Chunk nGRP: " + chunknGRP);
            Console.WriteLine("[LOG] Chunk nTRN: " + chunknTRN);
            Console.WriteLine("[LOG] Chunk nSHP: " + chunknSHP);
            Console.WriteLine("[LOG] Chunk MnTRN: " + chunkMnTRN);
            Console.WriteLine("[LOG] Chunk MnGRP: " + chunkMnGRP);

            int childrenChunkSize = chunkSIZE; //SIZE CHUNK
            childrenChunkSize += chunkXYZI; //XYZI CHUNK
            childrenChunkSize += chunknGRP; //nGRP CHUNK
            childrenChunkSize += chunknTRN; //nTRN CHUNK
            childrenChunkSize += chunknSHP;
            childrenChunkSize += chunkRGBA;
            childrenChunkSize += chunkMATL;
            childrenChunkSize += chunkMnTRN;
            childrenChunkSize += chunkMnGRP;

            return childrenChunkSize;
        }

        private int CountTotalBlocks()
        {
            int total = 0;

            for (int i = 0; i < _models.Count; i++)
            {
                Color[] colorsPalette = _models[i].Palette;
                for (int j = 0; j < _models[i].VoxelFrames.Count; j++)
                {
                    VoxelData data = _models[i].VoxelFrames[j];
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

        private int CountTotalTransforms()
        {
            return _models.Sum(t => t.TransformNodeChunks.Count);
        }

        private int CountTotalModels()
        {
            return _models.Sum(t => t.VoxelFrames.Count);
        }

        private int CountTotalGroups()
        {
            return _models.Sum(t => t.GroupNodeChunks.Count);
        }
        /// <summary>
        /// Main loop for write all chunks
        /// </summary>
        /// <param name="writer"></param>
        private int WriteChunks(BinaryWriter writer)
        {
            int byteWritten = 0;
            int RGBA = WritePaletteChunk(writer);

            int MATL = 0;
            for (int i = 0; i < 256; i++)
            {
                if (_usedIndexColors.ContainsKey(i))
                {
                    KeyValuePair<int, int> modelIndex = _usedIndexColors[i];
                    if (_models[modelIndex.Key].MaterialChunks.Count > modelIndex.Value - 1)
                    {
                        MATL += WriteMaterialChunk(writer, _models[modelIndex.Key].MaterialChunks[modelIndex.Value - 1], i + 1);
                    }
                }
                else if (_models[0].MaterialChunks.Count > 0)
                {
                    MATL += WriteMaterialChunk(writer, _models[0].MaterialChunks[0], i + 1);
                }
            }

            int SIZE = 0;
            int XYZI = 0;

            Console.WriteLine("[LOG] Step [1/2]: Started to write SIZE and XYZI...");
            using (var progressbar = new ProgressBar())
            {
                int totalModels = CountTotalModels();
                int indexProgression = 0;
                foreach (VoxModel model in _models)
                {
                    for (int j = 0; j < model.VoxelFrames.Count; j++)
                    {
                        SIZE += WriteSizeChunk(writer, model.VoxelFrames[j].GetVolumeSize());
                        XYZI += WriteXyziChunk(writer, model, j);
                        float progress = indexProgression / (float)totalModels;
                        progressbar.Report(progress);
                        indexProgression++;
                    }
                }
            }
            Console.WriteLine("[LOG] Done.");

            int nGRP = 0;
            int nTRN = 0;
            int nSHP = 0;

            int mnTRN = WriteMainTransformChunk(writer);
            int indexChunk = 2;
            List<int> mainGroupIds = new List<int>();
            mainGroupIds.Add(2);

            for (int i = 0; i < _models.Count - 1; i++)
            {
	            if (_models[i].TransformNodeChunks.Count != 0)
	            {
		            int max = _models[i].TransformNodeChunks.Max(t => t.Id);
		            int transformId = max + (max % 2 == 0 ? 2 : 1) + mainGroupIds.Last();
		            mainGroupIds.Add(transformId);
                }
            }

            int mnGRP = WriteMainGroupChunk(writer, mainGroupIds);

            Console.WriteLine("[LOG] Step [2/2]: Started to write nTRN, nGRP and nSHP chunks...");
            using (var progressbar = new ProgressBar())
            {
                int indexProgression = 0;
                int totalTransform = CountTotalTransforms();
                Dictionary<int, int> modelIds = new Dictionary<int, int>();
                Dictionary<int, int> shapeIds = new Dictionary<int, int>();
                int indexModel = 0;
                mainGroupIds.Clear();
                mainGroupIds.Add(2);
                for (int i = 0; i < _models.Count; i++)
                {
                    for (int j = 0; j < _models[i].TransformNodeChunks.Count; j++)
                    {
                        int childId = _models[i].TransformNodeChunks[j].ChildId;

                        int transformIndexUnique = indexChunk++;
                       
                        ShapeNodeChunk shapeNode = _models[i].ShapeNodeChunks.FirstOrDefault(t => t.Id == childId);
                        if (shapeNode != null)
                        {
                            int modelId = shapeNode.Models[0].ModelId + i;
                            int modelIndexUnique = modelId + (i * 2000); //Hack ...

                            if (!modelIds.ContainsKey(modelIndexUnique))
                            {
                                modelIds.Add(modelIndexUnique, indexModel);
                                indexModel++;
                            }

                            int shapeIndexUnique = (shapeNode.Id + ((i + 1) * 2000) + 2); //Hack
                            nTRN += WriteTransformChunk(writer, _models[i].TransformNodeChunks[j], transformIndexUnique,  shapeIds.ContainsKey(shapeIndexUnique) ? shapeIds[shapeIndexUnique] : indexChunk);

                            if (!shapeIds.ContainsKey(shapeIndexUnique))
                            {
                                shapeIds.Add(shapeIndexUnique, indexChunk);
                                nSHP += WriteShapeChunk(writer, indexChunk, modelIds[modelIndexUnique]);
                                indexChunk++;
                            }
                        }
                        else
                        {
                            GroupNodeChunk groupNode = _models[i].GroupNodeChunks.FirstOrDefault(t => t.Id == childId);

                            int groupUniqueIndex = indexChunk++;

                            List<int> childIds = groupNode.ChildIds.ToList();
                            for (int index = 0; index < childIds.Count; index++)
                            {
                                childIds[index] += mainGroupIds.Last();
                            }

                            nTRN += WriteTransformChunk(writer, _models[i].TransformNodeChunks[j], transformIndexUnique, groupUniqueIndex);
                            nGRP += WriteGroupChunk(writer, groupUniqueIndex, childIds);

                        }

                        progressbar.Report(indexProgression / (float)totalTransform);
                        indexProgression++;
                    }

                    int max = _models[i].TransformNodeChunks.Max(t => t.Id);
                    mainGroupIds.Add( max + (max % 2 == 0 ? 2 : 1) + mainGroupIds.Last());
                }
            }

            Console.WriteLine("[LOG] Written RGBA: " + RGBA);
            Console.WriteLine("[LOG] Written MATL: " + MATL);
            Console.WriteLine("[LOG] Written SIZE: " + SIZE);
            Console.WriteLine("[LOG] Written XYZI: " + XYZI);
            Console.WriteLine("[LOG] Written nGRP: " + nGRP);
            Console.WriteLine("[LOG] Written nTRN: " + nTRN);
            Console.WriteLine("[LOG] Written nSHP: " + nSHP);
            Console.WriteLine("[LOG] Written mnTRN: " + mnTRN);
            Console.WriteLine("[LOG] Written mnGRP: " + mnGRP);

            byteWritten = RGBA + MATL + SIZE + XYZI + nGRP + nTRN + nSHP + mnTRN + mnGRP;
            return byteWritten;
        }

        /// <summary>
        /// Write the main nTRN chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <returns></returns>
        private int WriteMainTransformChunk(BinaryWriter writer)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nTRN));
            writer.Write(28); //Main nTRN has always a size of 28 bytes
            writer.Write(0); //Child nTRN chunk size
            writer.Write(0); //ID of nTRN
            writer.Write(0); //ReadDICT size for attributes
            writer.Write(1); //Child ID
            writer.Write(-1); //Reserved ID
            writer.Write(0); //Layer ID
            writer.Write(1); //Read Array Size
            writer.Write(0); //ReadDICT size

            byteWritten += Encoding.UTF8.GetByteCount(nTRN) + 36;
            return byteWritten;
        }

        private int WriteMainGroupChunk(BinaryWriter writer, List<int> ids)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nGRP));
            writer.Write(12 + (4 * (ids.Count))); //nGRP chunk size
            writer.Write(0); //Child nGRP chunk size
            writer.Write(1); //ID of nGRP
            writer.Write(0); //Read DICT size for attributes (none)
            writer.Write(ids.Count);
            byteWritten += Encoding.UTF8.GetByteCount(nGRP) + 20;

            for (int i = 0; i < ids.Count; i++)
            {
                writer.Write(ids[i]); //Write the ID of child group
                byteWritten += 4;
            }


            return byteWritten;
        }

        /// <summary>
        /// Write nTRN chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private int WriteTransformChunk(BinaryWriter writer, TransformNodeChunk transformNode, int id, int childId)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nTRN));
            byteWritten += Encoding.UTF8.GetByteCount(nTRN);

            Vector3 worldPosition = transformNode.TranslationAt();
            string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;

            writer.Write(48 + Encoding.UTF8.GetByteCount(pos)
                            + Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt()))); //nTRN chunk size
            writer.Write(0); //nTRN child chunk size
            writer.Write(id); //ID
            writer.Write(0); //ReadDICT size for attributes (none)
            writer.Write(childId);//Child ID
            writer.Write(-1); //Reserved ID
            writer.Write(transformNode.LayerId); //Layer ID
            writer.Write(1); //Read Array Size
            writer.Write(2); //Read DICT Size (previously 1)

            writer.Write(2); //Read STRING size
            byteWritten += 40;

            writer.Write(Encoding.UTF8.GetBytes("_r"));
            writer.Write(Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt())));
            writer.Write(Encoding.UTF8.GetBytes(Convert.ToString((byte)transformNode.RotationAt())));

            byteWritten += Encoding.UTF8.GetByteCount("_r");
            byteWritten += 4;
            byteWritten += Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt()));


            writer.Write(2); //Read STRING Size
            writer.Write(Encoding.UTF8.GetBytes("_t"));
            writer.Write(Encoding.UTF8.GetByteCount(pos));
            writer.Write(Encoding.UTF8.GetBytes(pos));

            byteWritten += 4 + Encoding.UTF8.GetByteCount("_t") + 4 + Encoding.UTF8.GetByteCount(pos);
            return byteWritten;
        }


        /// <summary>
        /// Write nSHP chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private int WriteShapeChunk(BinaryWriter writer, int id, int indexModel)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nSHP));
            writer.Write(20); //nSHP chunk size
            writer.Write(0); //nSHP child chunk size
            writer.Write(id); //ID
            writer.Write(0);
            writer.Write(1);
            writer.Write(indexModel);
            writer.Write(0);

            byteWritten += Encoding.UTF8.GetByteCount(nSHP) + 28;
            return byteWritten;
        }

        /// <summary>
        /// Write nGRP chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WriteGroupChunk(BinaryWriter writer, int id, List<int> ids)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nGRP));
            writer.Write(12 + (4 * (ids.Count))); //nGRP chunk size
            writer.Write(0); //Child nGRP chunk size
            writer.Write(id); //ID of nGRP
            writer.Write(0); //Read DICT size for attributes (none)
            writer.Write(ids.Count);
            byteWritten += Encoding.UTF8.GetByteCount(nGRP) + 20;

            for (int i = 0; i < ids.Count; i++)
            {
                writer.Write(ids[i]); //Write the ID of child group
                byteWritten += 4;
            }


            return byteWritten;
        }

        /// <summary>
        /// Write SIZE chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WriteSizeChunk(BinaryWriter writer, Vector3 volumeSize)
        {
            int byteWritten = 0;

            writer.Write(Encoding.UTF8.GetBytes(SIZE));
            writer.Write(12); //Chunk Size (constant)
            writer.Write(0); //Child Chunk Size (constant)

            writer.Write((int)volumeSize.X); //Width
            writer.Write((int)volumeSize.Y); //Height
            writer.Write((int)volumeSize.Z); //Depth

            byteWritten += Encoding.UTF8.GetByteCount(SIZE) + 20;
            return byteWritten;
        }

        /// <summary>
        /// Write XYZI chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private int WriteXyziChunk(BinaryWriter writer, VoxModel model, int index)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(XYZI));
            //int testA = (model.VoxelFrames[index].Colors.Count(t => t != 0));
            //int testB = model.VoxelFrames[index].Colors.Length;
            writer.Write((model.VoxelFrames[index].Colors.Count(t => t != 0) * 4) + 4); //XYZI chunk size
            writer.Write(0); //Child chunk size (constant)
            writer.Write(model.VoxelFrames[index].Colors.Count(t => t != 0)); //Blocks count

            byteWritten += Encoding.UTF8.GetByteCount(XYZI) + 12;
            int count = 0;
            for (int y = 0; y < model.VoxelFrames[index].VoxelsTall; y++)
            {
                for (int z = 0; z < model.VoxelFrames[index].VoxelsDeep; z++)
                {
                    for (int x = 0; x < model.VoxelFrames[index].VoxelsWide; x++)
                    {
                        int PaletteIndex = model.VoxelFrames[index].Get(x, y, z);
                        Color color = model.Palette[PaletteIndex];

                        if (color != Color.Empty)
                        {
                            writer.Write((byte)(x % model.VoxelFrames[index].VoxelsWide));
                            writer.Write((byte)(y % model.VoxelFrames[index].VoxelsTall));
                            writer.Write((byte)(z % model.VoxelFrames[index].VoxelsDeep));

                            int i = _usedColors.IndexOf(color) + 1;
                            writer.Write((i != 0) ? (byte)i : (byte)1);
                            count++;

                            byteWritten += 4;
                        }

                    }
                }
            }

            return byteWritten;
        }

        /// <summary>
        /// Write RGBA chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WritePaletteChunk(BinaryWriter writer)
        {
            int byteCount = 0;
            writer.Write(Encoding.UTF8.GetBytes(RGBA));
            writer.Write(1024);
            writer.Write(0);

            byteCount += Encoding.UTF8.GetByteCount(RGBA) + 8;

            for (int i = 0; i < _usedColors.Count; i++)
            {
                Color color = _usedColors[i];
                writer.Write(color.R);
                writer.Write(color.G);
                writer.Write(color.B);
                writer.Write(color.A);
                byteCount += 4;
            }

            for (int i = (256 - _usedColors.Count); i >= 1; i--)
            {
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                byteCount += 4;
            }

            return byteCount;
        }


        /// <summary>
        /// Write MATL chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WriteMaterialChunk(BinaryWriter writer, MaterialChunk materialChunk, int index)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(MATL));
            writer.Write(GetMaterialPropertiesSize(materialChunk.Properties) + 8);
            writer.Write(0); //Child Chunk Size (constant)

            writer.Write(index); //Id
            writer.Write(materialChunk.Properties.Length); //ReadDICT size

            byteWritten += Encoding.UTF8.GetByteCount(MATL) + 16;

            foreach (KeyValue keyValue in materialChunk.Properties)
            {
                writer.Write(Encoding.UTF8.GetByteCount(keyValue.Key));
                writer.Write(Encoding.UTF8.GetBytes(keyValue.Key));
                writer.Write(Encoding.UTF8.GetByteCount(keyValue.Value));
                writer.Write(Encoding.UTF8.GetBytes(keyValue.Value));

                byteWritten += 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value);
            }

            return byteWritten;
        }

        private int GetMaterialPropertiesSize(KeyValue[] properties)
        {
            return properties.Sum(keyValue => 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value));
        }


        /// <summary>
        /// Count the size of all materials chunks
        /// </summary>
        /// <returns></returns>
        private int CountMaterialChunksize()
        {
            int usedIndexColor = 0;
            Console.WriteLine("[LOG] Started to create an optimized Palette...");
            int globalIndex = 0;
            using (ProgressBar progressBar = new ProgressBar())
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    VoxModel model = _models[i];
                    for (int j = 0; j < model.Palette.Length; j++)
                    {
                        Color color = model.Palette[j];
                        if (_usedColors.Count < 256 && !_usedColors.Contains(color) && color != Color.Empty && _models[i].ColorUsed.Contains(j))
                        {
                            _usedIndexColors[usedIndexColor] = new KeyValuePair<int, int>(i, j);
                            _usedColors.Add(color);
                            usedIndexColor++;
                        }

                        globalIndex++;
                        progressBar.Report(globalIndex / (float)(_models.Count * 256));
                    }
                }
            }
            Console.WriteLine("[LOG] Done.");

            int size = 0;
            for (int i = 0; i < 256; i++)
            {
                if (_usedIndexColors.ContainsKey(i))
                {
                    KeyValuePair<int, int> modelIndex = _usedIndexColors[i];
                    if (_models[modelIndex.Key].MaterialChunks.Count > modelIndex.Value - 1)
                    {
                        size += Encoding.UTF8.GetByteCount(MATL) + 16;

                        size += _models[modelIndex.Key].MaterialChunks[modelIndex.Value - 1].Properties.Sum(keyValue => 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value));
                    }
                }
                else
                {
					if (_models[0].MaterialChunks.Count > 0)
					{
						size += Encoding.UTF8.GetByteCount(MATL) + 16;
						size += _models[0].MaterialChunks[0].Properties.Sum(keyValue => 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value));
					}
				}
            }

            return size;
        }


        /// <summary>
        /// Count the size of all nTRN chunks
        /// </summary>
        /// <returns></returns>
        private int CountTransformChunkSize()
        {
            int size = 0;
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 0; j < _models[i].TransformNodeChunks.Count; j++)
                {
                    Vector3 worldPosition = _models[i].TransformNodeChunks[j].TranslationAt();
                    Rotation rotation = _models[i].TransformNodeChunks[j].RotationAt();

                    string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;

                    size += Encoding.UTF8.GetByteCount(nTRN);
                    size += 40;


                    size += Encoding.UTF8.GetByteCount("_r");
                    size += 4;
                    size += Encoding.UTF8.GetByteCount(Convert.ToString((byte)rotation));
                    size += 4 + Encoding.UTF8.GetByteCount("_t") + 4 + Encoding.UTF8.GetByteCount(pos);
                }
            }

            return size;
        }

        /// <summary>
        /// Count the size of all nSHP chunks
        /// </summary>
        /// <returns></returns>
        private int CountShapeChunkSize()
        {
            int size = 0;
            List<int> shapeIds = new List<int>();
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 0; j < _models[i].ShapeNodeChunks.Count; j++)
                {
                    ShapeNodeChunk shapeNode = _models[i].ShapeNodeChunks[j];
                    int id = (shapeNode.Id + (i * 2000) + 2);
                    if (!shapeIds.Contains(id))
                    {
                        shapeIds.Add(id);
                        size += Encoding.UTF8.GetByteCount(nSHP) + 28;
                    }
                }
            }
            return size;
        }


        /// <summary>
        /// Count the size of all nGRP chunks
        /// </summary>
        /// <returns></returns>
        private int CountGroupChunkSize()
        {
            int byteWritten = 0;
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 0; j < _models[i].GroupNodeChunks.Count; j++)
                {
                    byteWritten += Encoding.UTF8.GetByteCount(nGRP) + 20;
                    byteWritten += _models[i].GroupNodeChunks[j].ChildIds.ToList().Sum(id => 4);
                }
            }

            return byteWritten;
        }

        /// <summary>
        /// Count the size of the main nGRP chunk
        /// </summary>
        /// <returns></returns>
        private int CountMainGroupChunkSize()
        {
            int byteWritten = 0;
            byteWritten += Encoding.UTF8.GetByteCount(nGRP) + 20;
            byteWritten += _models.Sum(id => 4);

            return byteWritten;
        }
    }
}
