// Touchable point clouds
// by Norbert Pape and Simon Speiser
// using point clouds imported with Keijiro's
// norbertpape111@gmail.com
// Extension of Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Random=UnityEngine.Random;
using UnityEditor.AssetImporters;

using System.Diagnostics;


namespace PcxTouchable
{
    [ScriptedImporter(1, "ply")]
    class PlyImporter : ScriptedImporter
    {
        #region ScriptedImporter implementation


        public enum Style { Normal = 2, Heal = 16 }

        [SerializeField] bool _healingStyle = false;
        [SerializeField] int _pointCountCap = 300000;

        public override void OnImportAsset(AssetImportContext context)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            // ComputeBuffer container
            // Create a prefab with PointCloudRenderer.
            
            var gameObject = new GameObject();
            var data = ImportAsPointCloudData(context.assetPath);
            if (data == null) return;
            data.healing = _healingStyle;
            var renderer = gameObject.AddComponent<PointCloudRenderer>();
            renderer.sourceData = data;
            var boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = data.boundingBox[1] - data.boundingBox[0];
            boxCollider.center = data.boundingBox[0] + 0.5f * boxCollider.size;
            context.AddObjectToAsset("prefab", gameObject);
            context.AddObjectToAsset("data", data);
            context.SetMainObject(gameObject);
        }

        #endregion

        #region Internal data structure

        enum DataProperty {
            Invalid,
            R8, G8, B8, A8,
            R16, G16, B16, A16,
            SingleX, SingleY, SingleZ,
            DoubleX, DoubleY, DoubleZ,
            SingleNX, SingleNY, SingleNZ,
            DoubleNX, DoubleNY, DoubleNZ,
            Data8, Data16, Data32, Data64
        }

        static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.SingleNX: return 4;
                case DataProperty.SingleNY: return 4;
                case DataProperty.SingleNZ: return 4;
                case DataProperty.DoubleNX: return 8;
                case DataProperty.DoubleNY: return 8;
                case DataProperty.DoubleNZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
            }
            return 0;
        }

        class DataHeader
        {
            public List<DataProperty> properties = new List<DataProperty>();
            public int vertexCount = -1;
        }

        static uint EncodeColor(byte r, byte g, byte b, byte a)
        {
            return (uint) a << 24 | ((uint) b << 16) | ( (uint) g << 8) | (uint) r ;
        }

        class DataBody
        {
            public List<Vector3> vertices;
            public Vector3[] boundingBox;
            public List<uint> colors;
            public Vector3 center;
            public float boundingRadius;

            public DataBody()
            {
                vertices = new List<Vector3>();
                boundingBox =  new Vector3[2] { Vector3.zero, Vector3.zero}; //two opposite corners
                colors = new List<uint>();
                center = new Vector3();
                boundingRadius = 0f;
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(EncodeColor(r, g, b, a));
            }

            public void Shuffle()
            {
                for (int t = 0; t < vertices.Count; t++ )
                {
                    var tmp = vertices[t];
                    var tmpCol = colors[t];
                    int r = Random.Range(t, vertices.Count);
                    vertices[t] = vertices[r];
                    vertices[r] = tmp;
                    colors[t] = colors[r];
                    colors[r] = tmpCol;
                }
            }
        }

        #endregion

        #region Reader implementation

        int ToInt( bool value)
        {
            return value ? 1 : 0;
        }

        PointCloudData ImportAsPointCloudData(string path)
        {
            try
            {
                uint style = 0;
                if (_healingStyle) style = (uint) (ToInt(_healingStyle) * (int) Style.Heal);
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<PointCloudData>();
                data.Initialize(body.vertices, body.colors, body.boundingBox, style, body.center, body.boundingRadius);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        private DataBody Resize( DataBody c)
        {
            Vector3 newCenter = 0.5f * (c.boundingBox[1] + c.boundingBox[0]);
            c.center += newCenter;
            c.boundingRadius = 0f;
            c.boundingBox[0] -= newCenter;
            c.boundingBox[1] -= newCenter;
            for (int i = 0; i < c.vertices.Count; i++)
            {
                c.vertices[i] -= newCenter;
                float n = c.vertices[i].magnitude;
                if (n > c.boundingRadius) c.boundingRadius = n;
            }
            return c;
        } 

        DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            // Read header contents.
            for (var skip = false;;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices.
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    switch (col[2])
                    {
                        case "red"  : prop = DataProperty.R8; break;
                        case "green": prop = DataProperty.G8; break;
                        case "blue" : prop = DataProperty.B8; break;
                        case "alpha": prop = DataProperty.A8; break;
                        case "x"    : prop = DataProperty.SingleX; break;
                        case "y"    : prop = DataProperty.SingleY; break;
                        case "z"    : prop = DataProperty.SingleZ; break;
                        case "nx"    : prop = DataProperty.SingleNX; break;
                        case "ny"    : prop = DataProperty.SingleNY; break;
                        case "nz"    : prop = DataProperty.SingleNZ; break;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                             col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int"   || col[1] == "uint"   || col[1] == "float" ||
                             col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64"  || col[1] == "uint64" ||
                             col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleNX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleNY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleNZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return data;
        }

        DataBody ReadDataBody(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody();

            float x = 0, y = 0, z = 0;
            float nx = 0f, ny = 0f, nz = 0f;
            Byte r = 255, g = 255, b = 255, a = 255;
            Vector3[] bbox = {Vector3.zero, Vector3.zero};
            float bRadius = 0f;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = reader.ReadByte(); break;
                        case DataProperty.G8: g = reader.ReadByte(); break;
                        case DataProperty.B8: b = reader.ReadByte(); break;
                        case DataProperty.A8: a = reader.ReadByte(); break;

                        case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                        case DataProperty.SingleX: x = reader.ReadSingle(); break;
                        case DataProperty.SingleY: y = reader.ReadSingle(); break;
                        case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                        case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                        case DataProperty.SingleNX: nx = reader.ReadSingle(); break;
                        case DataProperty.SingleNY: ny = reader.ReadSingle(); break;
                        case DataProperty.SingleNZ: nz = reader.ReadSingle(); break;

                        case DataProperty.DoubleNX: nx = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleNY: ny = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleNZ: nz = (float)reader.ReadDouble(); break;

                        case DataProperty.Data8: reader.ReadByte(); break;
                        case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                        case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                        case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                    }
                }
                Vector3 pt = new Vector3(x,y,z);
                bbox[0] = Vector3.Min(pt, bbox[0]);
                bbox[1] = Vector3.Max(pt, bbox[1]);
                bRadius = Mathf.Max(bRadius, pt.magnitude);
                data.AddPoint(x, y, z, r, g, b, a);
            }

            data.Shuffle();
            if (header.vertexCount > _pointCountCap) {
                var dataReduced = new DataBody();
                dataReduced.vertices = data.vertices.Take(_pointCountCap).ToList<Vector3>();
                dataReduced.colors = data.colors.Take(_pointCountCap).ToList<uint>();
                data = dataReduced;
            }

            data.boundingBox = bbox;
            data.boundingRadius = bRadius;
            data.center = new Vector3(0f,0f,0f);
            data = Resize(data);
            return data;
        }
    }

    #endregion
}
