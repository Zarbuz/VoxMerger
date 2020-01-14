using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoxMerger.Extensions;
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
                    return false;
                }
            }

            return true;
        }

        private int CountChildrenSize()
        {
            _countSize = CountTotalTransforms();
            _totalBlockCount = CountTotalBlocks();
            int totalModels = CountTotalModels();
            Console.WriteLine("[INFO] Total blocks: " + _totalBlockCount);

            int chunkSIZE = 24 * totalModels; //24 = 12 bytes for header and 12 bytes of content
            int chunkXYZI = (16 * totalModels) + _totalBlockCount * 4; //16 = 12 bytes for header and 4 for the voxel count + (number of voxels) * 4
            int chunknTRNMain = 40; //40 = 
            int chunknGRP = 24 + _countSize * 4;
            int chunknTRN = CountTransformChunkSize();
            int chunknSHP = CountShapeChunkSize();
            int chunkRGBA = 1024 + 12;
            int chunkMATL = CountMaterialChunkSize();


            Console.WriteLine("[LOG] Chunk RGBA: " + chunkRGBA);
            Console.WriteLine("[LOG] Chunk MATL: " + chunkMATL);
            Console.WriteLine("[LOG] Chunk SIZE: " + chunkSIZE);
            Console.WriteLine("[LOG] Chunk XYZI: " + chunkXYZI);
            Console.WriteLine("[LOG] Chunk nTRN Main: " + chunknTRNMain);
            Console.WriteLine("[LOG] Chunk nGRP: " + chunknGRP);
            Console.WriteLine("[LOG] Chunk nTRN: " + chunknTRN);
            Console.WriteLine("[LOG] Chunk nSHP: " + chunknSHP);

            int childrenChunkSize = chunkSIZE; //SIZE CHUNK
            childrenChunkSize += chunkXYZI; //XYZI CHUNK
            childrenChunkSize += chunknTRNMain; //First nTRN CHUNK (constant)
            childrenChunkSize += chunknGRP; //nGRP CHUNK
            childrenChunkSize += chunknTRN; //nTRN CHUNK
            childrenChunkSize += chunknSHP;
            childrenChunkSize += chunkRGBA;
            childrenChunkSize += chunkMATL;

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

        private int CountTotalTransforms()
        {
            return _models.Sum(t => t.transformNodeChunks.Count - 1);
        }

        private int CountTotalModels()
        {
            return _models.Sum(t => t.voxelFrames.Count);
        }

        private int CountTotalRegions()
        {
            return _models.Sum(t => t.groupNodeChunks.Count);
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
                    MATL += WriteMaterialChunk(writer, _models[modelIndex.Key].materialChunks[modelIndex.Value - 1], i + 1);
                }
                else
                {
                    MATL += WriteMaterialChunk(writer, _models[0].materialChunks[0], i + 1);
                }
            }

            int SIZE = 0;
            int XYZI = 0;

            using (var progressbar = new ProgressBar())
            {
                int totalModels = CountTotalModels();
                int indexProgression = 0;
                foreach (VoxModel model in _models)
                {
                    for (int j = 0; j < model.voxelFrames.Count; j++)
                    {
                        SIZE += WriteSizeChunk(writer, model.voxelFrames[j].GetVolumeSize());
                        XYZI += WriteXyziChunk(writer, model, j);
                        float progress = indexProgression / (float)totalModels;
                        progressbar.Report(progress);
                        indexProgression++;
                    }
                }
            }

            int nTRNmain = WriteMainTranformNode(writer);
            int nGRP = WriteGroupChunk(writer);

            int index = 0;
            Dictionary<int, int> modelIds = new Dictionary<int, int>();
            int indexModel = 0;
            int nTRN = 0;
            int nSHP = 0;
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 1; j < _models[i].transformNodeChunks.Count; j++)
                {
                    int childId = _models[i].transformNodeChunks[j].childId;

                    ShapeNodeChunk shapeNode = _models[i].shapeNodeChunks.FirstOrDefault(t => t.id == childId);
                    if (shapeNode != null)
                    {
                        int modelId = shapeNode.models[0].modelId + i;

                        if (!modelIds.ContainsKey(modelId + i))
                        {
                            modelIds.Add(modelId + i, indexModel);
                            indexModel++;
                        }

                        nTRN += WriteTransformChunk(writer, _models[i].transformNodeChunks[j], index);
                        nSHP += WriteShapeChunk(writer, index, modelIds[modelId + i]);

                        index++;
                    }
                }
            }

            Console.WriteLine("[LOG] Written RGBA: " + RGBA);
            Console.WriteLine("[LOG] Written MATL: " + MATL);
            Console.WriteLine("[LOG] Written SIZE: " + SIZE);
            Console.WriteLine("[LOG] Written XYZI: " + XYZI);
            Console.WriteLine("[LOG] Written main nTRN: " + nTRNmain);
            Console.WriteLine("[LOG] Written nGRP: " + nGRP);
            Console.WriteLine("[LOG] Written nTRN: " + nTRN);
            Console.WriteLine("[LOG] Written nSHP: " + nSHP);

            byteWritten = RGBA + MATL + SIZE + XYZI + nTRNmain + nGRP + nTRN + nSHP;
            return byteWritten;
        }

        /// <summary>
        /// Write nTRN chunk
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="index"></param>
        private int WriteTransformChunk(BinaryWriter writer, TransformNodeChunk transformNode, int index)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nTRN));
            byteWritten += Encoding.UTF8.GetByteCount(nTRN);

            Vector3 worldPosition = transformNode.TranslationAt();
            string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;

            writer.Write(48 + Encoding.UTF8.GetByteCount(pos)
                            + Encoding.UTF8.GetByteCount(Convert.ToString((byte)transformNode.RotationAt()))); //nTRN chunk size
            writer.Write(0); //nTRN child chunk size
            writer.Write(2 * index + 2); //ID
            writer.Write(0); //ReadDICT size for attributes (none)
            writer.Write(2 * index + 3);//Child ID
            writer.Write(-1); //Reserved ID
            writer.Write(transformNode.layerId); //Layer ID
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
        private int WriteShapeChunk(BinaryWriter writer, int index, int indexModel)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nSHP));
            writer.Write(20); //nSHP chunk size
            writer.Write(0); //nSHP child chunk size
            writer.Write(2 * index + 3); //ID
            writer.Write(0);
            writer.Write(1);
            writer.Write(indexModel);
            writer.Write(0);

            byteWritten += Encoding.UTF8.GetByteCount(nSHP) + 28;
            return byteWritten;
        }


        /// <summary>
        /// Write the main trande node chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WriteMainTranformNode(BinaryWriter writer)
        {
            int byteWritten = 0;
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

            byteWritten += Encoding.UTF8.GetByteCount(nTRN) + 36;
            return byteWritten;
        }

        /// <summary>
        /// Write nGRP chunk
        /// </summary>
        /// <param name="writer"></param>
        private int WriteGroupChunk(BinaryWriter writer)
        {
            int byteWritten = 0;
            writer.Write(Encoding.UTF8.GetBytes(nGRP));
            writer.Write(16 + (4 * (_countSize - 1))); //nGRP chunk size
            writer.Write(0); //Child nGRP chunk size
            writer.Write(1); //ID of nGRP
            writer.Write(0); //Read DICT size for attributes (none)
            writer.Write(_countSize);
            byteWritten += Encoding.UTF8.GetByteCount(nGRP) + 20;

            for (int i = 0; i < _countSize; i++)
            {
                writer.Write((2 * i) + 2); //id for childrens (start at 2, increment by 2)
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
            //int testA = (model.voxelFrames[index].Colors.Count(t => t != 0));
            //int testB = model.voxelFrames[index].Colors.Length;
            writer.Write((model.voxelFrames[index].Colors.Count(t => t != 0) * 4) + 4); //XYZI chunk size
            writer.Write(0); //Child chunk size (constant)
            writer.Write(model.voxelFrames[index].Colors.Count(t => t != 0)); //Blocks count

            byteWritten += Encoding.UTF8.GetByteCount(XYZI) + 12;
            int count = 0;
            for (int y = 0; y < model.voxelFrames[index].VoxelsTall; y++)
            {
                for (int z = 0; z < model.voxelFrames[index].VoxelsDeep; z++)
                {
                    for (int x = 0; x < model.voxelFrames[index].VoxelsWide; x++)
                    {
                        int paletteIndex = model.voxelFrames[index].Get(x, y, z);
                        Color color = model.palette[paletteIndex];

                        if (color != Color.Empty)
                        {
                            writer.Write((byte)(x % model.voxelFrames[index].VoxelsWide));
                            writer.Write((byte)(y % model.voxelFrames[index].VoxelsTall));
                            writer.Write((byte)(z % model.voxelFrames[index].VoxelsDeep));

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
            _usedColors = new List<Color>(256);

            int usedIndexColor = 0;
            int globalIndex = 0;
            Console.WriteLine("[LOG] Started to create an optimized palette...");
            using (ProgressBar progressBar = new ProgressBar())
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    VoxModel model = _models[i];
                    for (int j = 0; j < model.palette.Length; j++)
                    {
                        Color color = model.palette[j];
                        if (_usedColors.Count < 256 && !_usedColors.Contains(color) && color != Color.Empty && _models[i].colorUsed.Contains((byte) j))
                        {
                            _usedIndexColors[usedIndexColor] = new KeyValuePair<int, int>(i, j);
                            _usedColors.Add(color);
                            writer.Write(color.R);
                            writer.Write(color.G);
                            writer.Write(color.B);
                            writer.Write(color.A);
                            byteCount += 4;
                            usedIndexColor++;
                        }

                        globalIndex++;
                        progressBar.Report(globalIndex / (float)(_models.Count * 256));
                    }
                }
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
            writer.Write(GetMaterialPropertiesSize(materialChunk.properties) + 8);
            writer.Write(0); //Child Chunk Size (constant)

            writer.Write(index); //Id
            writer.Write(12); //ReadDICT size

            byteWritten += Encoding.UTF8.GetByteCount(MATL) + 16;

            foreach (KeyValue keyValue in materialChunk.properties)
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
        private int CountMaterialChunkSize()
        {
            List<Color> usedColor = new List<Color>();
            Dictionary<int, KeyValuePair<int, int>> usedIndexColors = new Dictionary<int, KeyValuePair<int, int>>();

            int usedIndexColor = 0;

            for (int i = 0; i < _models.Count; i++)
            {
                VoxModel model = _models[i];
                for (int j = 0; j < model.palette.Length; j++)
                {
                    Color color = model.palette[j];
                    if (usedColor.Count < 256 && !usedColor.Contains(color) && color != Color.Empty && _models[i].colorUsed.Contains((byte)j))
                    {
                        usedIndexColors[usedIndexColor] = new KeyValuePair<int, int>(i, j);
                        usedColor.Add(color);
                        usedIndexColor++;
                    }

                }
            }

            int size = 0;
            for (int i = 0; i < 256; i++)
            {
                size += Encoding.UTF8.GetByteCount(MATL) + 16;

                if (usedIndexColors.ContainsKey(i))
                {
                    KeyValuePair<int, int> modelIndex = usedIndexColors[i];
                    size += _models[modelIndex.Key].materialChunks[modelIndex.Value - 1].properties.Sum(keyValue => 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value));
                }
                else
                {
                    size += _models[0].materialChunks[0].properties.Sum(keyValue => 8 + Encoding.UTF8.GetByteCount(keyValue.Key) + Encoding.UTF8.GetByteCount(keyValue.Value));
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
                for (int j = 1; j < _models[i].transformNodeChunks.Count; j++)
                {
                    int childId = _models[i].transformNodeChunks[j].childId;

                    ShapeNodeChunk shapeNode = _models[i].shapeNodeChunks.FirstOrDefault(t => t.id == childId);
                    if (shapeNode != null)
                    {
                        Vector3 worldPosition = _models[i].transformNodeChunks[j].TranslationAt();
                        Rotation rotation = _models[i].transformNodeChunks[j].RotationAt();

                        string pos = worldPosition.X + " " + worldPosition.Y + " " + worldPosition.Z;

                        size += Encoding.UTF8.GetByteCount(nTRN);
                        size += 40;


                        size += Encoding.UTF8.GetByteCount("_r");
                        size += 4;
                        size += Encoding.UTF8.GetByteCount(Convert.ToString((byte)rotation));
                        size += 4 + Encoding.UTF8.GetByteCount("_t") + 4 + Encoding.UTF8.GetByteCount(pos);

                    }
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
            for (int i = 0; i < _models.Count; i++)
            {
                for (int j = 1; j < _models[i].transformNodeChunks.Count; j++)
                {
                    int childId = _models[i].transformNodeChunks[j].childId;

                    ShapeNodeChunk shapeNode = _models[i].shapeNodeChunks.FirstOrDefault(t => t.id == childId);
                    if (shapeNode != null)
                    {
                        size += Encoding.UTF8.GetByteCount(nSHP) + 28;
                    }
                }
            }
            return size;
        }
    }
}
