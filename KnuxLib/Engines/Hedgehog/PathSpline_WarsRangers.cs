﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KnuxLib.Engines.Hedgehog
{
    // Based on https://github.com/blueskythlikesclouds/SkythTools/tree/master/Sonic%20Forces/Path%20Scripts
    // TODO: Figure out and properly read the k-d tree data.
    // TODO: Import Blender 4.x OBJ splines.
    // TODO: Check to see if Frontiers does anything other than the type differently, if so, handle it with the FormatVersion check.
    public class PathSpline_WarsRangers : FileBase
    {
        // Generic VS stuff to allow creating an object that instantly loads a file.
        public PathSpline_WarsRangers() { }
        public PathSpline_WarsRangers(string filepath, FormatVersion version = FormatVersion.Wars, bool export = false)
        {
            Load(filepath, version);

            if (export)
                ExportOBJ($@"{Path.GetDirectoryName(filepath)}\{Path.GetFileNameWithoutExtension(filepath)}.obj");
        }

        // Classes for this format.
        public enum FormatVersion
        {
            Wars = 0,
            Rangers = 1
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum SplineTypeWars : ulong
        {
            Default = 0,
            SideView = 1,
            GrindRail = 2
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum SplineTypeRangers : ulong
        {
            Default = 0,
            GrindRail = 1,
            SideView = 2
        }

        public class SplinePath
        {
            /// <summary>
            /// The name of this spline.
            /// </summary>
            public string Name { get; set; } = "objpath_001";

            /// <summary>
            /// An unknown short value.
            /// TODO: What is this? objpath_001 in Forces' w7b02_path.path is the only time this is 0 rather than 1.
            /// TODO: Could this indicate whether a spline is open or not? If this was the case then objpath_003 in Forces' w7a03_path.path and svpath_320_SV + svpath_321_SV in Frontiers' w6d08_sv_path.path should have it too? 
            /// </summary>
            public ushort UnknownUShort_1 { get; set; } = 0x01;

            /// <summary>
            /// This spline's distance array.
            /// </summary>
            public float[] Distance { get; set; } = Array.Empty<float>();

            /// <summary>
            /// This spline's knot points.
            /// </summary>
            public Vector3[] Knots { get; set; } = Array.Empty<Vector3>();

            /// <summary>
            /// This spline's up vector array.
            /// </summary>
            public Vector3[] UpVector { get; set; } = Array.Empty<Vector3>();

            /// <summary>
            /// This spline's forward vector array.
            /// </summary>
            public Vector3[] ForwardVector { get; set; } = Array.Empty<Vector3>();

            /// <summary>
            /// This spline's knot points for double splines.
            /// </summary>
            public Vector3[]? DoubleKnots { get; set; }

            /// <summary>
            /// This spline's axis aligned bounding box.
            /// </summary>
            public AABB AxisAlignedBoundingBox { get; set; } = new();

            /// <summary>
            /// This spline's type.
            /// </summary>
            public object Type { get; set; } = SplineTypeWars.Default;

            /// <summary>
            /// This path's UID, if it has one.
            /// </summary>
            public ulong? UID { get; set; }

            /// <summary>
            /// This path's k-d tree.
            /// </summary>
            public KDTree KDTree { get; set; } = new();

            public override string ToString() => Name;
        }

        public class KDTree
        {
            /// <summary>
            /// An unknown integer value.
            /// TODO: What is this?
            /// </summary>
            public uint UnknownUInt32_1 { get; set; }

            /// <summary>
            /// An unknown integer value.
            /// TODO: What is this?
            /// </summary>
            public uint UnknownUInt32_2 { get; set; }

            /// <summary>
            /// An unknown chunk of data.
            /// TODO: What is this?
            /// </summary>
            public byte[] UnknownData_1 { get; set; } = Array.Empty<byte>();

            /// <summary>
            /// An unknown set of values, consisting of two integer values each.
            /// TODO: What is this?
            /// </summary>
            public List<uint[]> UnknownData_2 { get; set; } = new();

            /// <summary>
            /// An unknown list of integer values.
            /// TODO: What is this?
            /// </summary>
            public uint[] UnknownData_3 = Array.Empty<uint>();
        }

        // Actual data presented to the end user.
        public List<SplinePath> Data = new();

        // HedgeLib# BinaryReader specific variables.
        // Set up HedgeLib#'s BINAV2Header.
        public HedgeLib.Headers.BINAHeader Header = new BINAv2Header(210);

        // Set up the Signature we expect.
        public new const string Signature = "HTAP";

        /// <summary>
        /// Loads and parses this format's file.
        /// </summary>
        /// <param name="filepath">The path to the file to load and parse.</param>
        /// <param name="version">The game version to read this file as.</param>
        public void Load(string filepath, FormatVersion version = FormatVersion.Wars)
        {
            // Set up HedgeLib#'s BINAReader and read the BINAV2 header.
            HedgeLib.IO.BINAReader reader = new(File.OpenRead(filepath));
            Header = reader.ReadHeader();

            // Check this file's signature.
            string signature = reader.ReadSignature();
            if (signature != Signature)
                throw new Exception($"Invalid signature, got '{signature}', expected '{Signature}'.");

            // Skip an unknown value that is always 0x200.
            reader.JumpAhead(0x04);

            // Read the amount of paths in this file.
            ulong pathCount = reader.ReadUInt64();

            // Read the offset to this file's path table.
            long pathTableOffset = reader.ReadInt64();

            // Jump to this file's path table.
            reader.JumpTo(pathTableOffset, false);

            // Loop through each path in this file.
            for (ulong pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                // Set up a new path entry.
                SplinePath path = new();

                // Read this path's name.
                path.Name = Helpers.ReadNullTerminatedStringTableEntry(reader);

                // Read an unknown ushort value that is always 1 except for a single instance.
                path.UnknownUShort_1 = reader.ReadUInt16();

                // Read the count of spline knots for this path.
                ushort knotCount = reader.ReadUInt16();

                // Skip a floating point value which is always the same as the final value in the distance array.
                reader.JumpAhead(0x04);

                // Read the offset to an unknown table (of knotCount length) of booleans that are always true.
                long unknownBooleanTableOffset = reader.ReadInt64();

                // Read the offset to this path's distance array.
                long distanceOffset = reader.ReadInt64();

                // Read the offset to this path's spline knot array.
                long knotOffset = reader.ReadInt64();

                // Read the offset to this path's up vector array.
                long upVectorOffset = reader.ReadInt64();

                // Read the offset to this path's forward vector array.
                long forwardVectorOffset = reader.ReadInt64();

                // Read offset to this path's double spline knot count.
                ulong doubleKnotCount = reader.ReadUInt64();

                // Read offset to this path's double spline knot array.
                long doubleKnotOffset = reader.ReadInt64();

                // Read this path's axis aligned bounding box.
                path.AxisAlignedBoundingBox.Min = Helpers.ReadHedgeLibVector3(reader);
                path.AxisAlignedBoundingBox.Max = Helpers.ReadHedgeLibVector3(reader);

                // Read the count of type entries in this path, usually 2 ("type" and "uid") but some only have 1.
                ulong typeCount = reader.ReadUInt64();

                // Read the offset to this path's type data.
                long typeOffset = reader.ReadInt64();

                // Skip an unknown value that is always 0.
                reader.JumpAhead(0x08);

                // Read the offset to this path's k-d tree.
                long kdTreeOffset = reader.ReadInt64();

                // Save our position so we can jump back for the next path.
                long position = reader.BaseStream.Position;

                // Jump to the distance array's offset.
                reader.JumpTo(distanceOffset, false);

                // Initialise this path's distance array.
                path.Distance = new float[knotCount];

                // Read each value in this path's distance array.
                for (int distanceIndex = 0; distanceIndex < knotCount; distanceIndex++)
                    path.Distance[distanceIndex] = reader.ReadSingle();

                // Jump to the spline knot array's offset.
                reader.JumpTo(knotOffset, false);

                // Initialise this path's spline knot array.
                path.Knots = new Vector3[knotCount];

                // Read each knot for this spline.
                for (int knotIndex = 0; knotIndex < knotCount; knotIndex++)
                    path.Knots[knotIndex] = Helpers.ReadHedgeLibVector3(reader);

                // Jump to the up vector array's offset.
                reader.JumpTo(upVectorOffset, false);

                // Initialise this path's up vector array.
                path.UpVector = new Vector3[knotCount];

                // Read each value in this path's up vector array.
                for (int upVectorIndex = 0; upVectorIndex < knotCount; upVectorIndex++)
                    path.UpVector[upVectorIndex] = Helpers.ReadHedgeLibVector3(reader);

                // Jump to the forward vector array's offset.
                reader.JumpTo(forwardVectorOffset, false);

                // Initialise this path's forward vector array.
                path.ForwardVector = new Vector3[knotCount];

                // Read each value in this path's forward vector array.
                for (int forwardVectorIndex = 0; forwardVectorIndex < knotCount; forwardVectorIndex++)
                    path.ForwardVector[forwardVectorIndex] = Helpers.ReadHedgeLibVector3(reader);

                // Only handle the double knot data if there is any, one path in Frontiers doesn't have any.
                if (doubleKnotCount != 0)
                {
                    // Jump to the double spline knot array's offset.
                    reader.JumpTo(doubleKnotOffset, false);

                    // Initialise this path's double spline knot array.
                    path.DoubleKnots = new Vector3[doubleKnotCount];

                    // Read each knot for this double spline.
                    for (ulong doubleKnotIndex = 0; doubleKnotIndex < doubleKnotCount; doubleKnotIndex++)
                        path.DoubleKnots[doubleKnotIndex] = Helpers.ReadHedgeLibVector3(reader);
                }

                // Jump to this path's type data offset.
                reader.JumpTo(typeOffset, false);

                // Skip an offset that always points to the word "type" in the string table.
                reader.JumpAhead(0x08);

                // Skip an unknown value of 0.
                reader.JumpAhead(0x08);

                // Read this path's type.
                switch (version)
                {
                    case FormatVersion.Wars: path.Type = (SplineTypeWars)reader.ReadUInt64(); break;
                    case FormatVersion.Rangers: path.Type = (SplineTypeRangers)reader.ReadUInt64(); break;
                }

                // If there is 2 types in this path, then read the UID entry.
                if (typeCount == 2)
                {
                    // Skip an offset that always points to the word "uid" in the string table.
                    reader.JumpAhead(0x08);

                    // Skip an unknown value of 0.
                    reader.JumpAhead(0x08);

                    // Read this path's UID value.
                    path.UID = reader.ReadUInt64();
                }

                #region TODO: Properly reverse engineer the k-d tree's data.
                // Jump to this path's k-d tree.
                reader.JumpTo(kdTreeOffset, false);
                path.KDTree.UnknownUInt32_1 = reader.ReadUInt32();

                path.KDTree.UnknownUInt32_2 = reader.ReadUInt32(); 

                long UnknownData_1_Offset = reader.ReadInt64();

                ulong UnknownData_2_Count = reader.ReadUInt64();

                long UnknownData_2_Offset = reader.ReadInt64();

                ulong UnknownData_3_Count = reader.ReadUInt64();

                long UnknownData_3_Offset = reader.ReadInt64();

                reader.JumpTo(UnknownData_1_Offset, false);

                path.KDTree.UnknownData_1 = reader.ReadBytes((int)(UnknownData_2_Offset - UnknownData_1_Offset));

                reader.JumpTo(UnknownData_2_Offset, false);

                for (ulong unknownIndex = 0; unknownIndex < UnknownData_2_Count; unknownIndex++)
                {
                    uint[] unknownValues = new uint[2];

                    unknownValues[0] = reader.ReadUInt32();
                    unknownValues[1] = reader.ReadUInt32();

                    path.KDTree.UnknownData_2.Add(unknownValues);
                }

                path.KDTree.UnknownData_3 = new uint[(int)UnknownData_3_Count];

                reader.JumpTo(UnknownData_3_Offset, false);

                for (ulong unknownIndex = 0; unknownIndex < UnknownData_3_Count; unknownIndex++)
                    path.KDTree.UnknownData_3[unknownIndex] = reader.ReadUInt32();
                #endregion

                // Save this path.
                Data.Add(path);

                // Jump back to read the next path.
                reader.JumpTo(position);
            }

            // Close HedgeLib#'s BINAReader.
            reader.Close();
        }

        /// <summary>
        /// Saves this format's file.
        /// </summary>
        /// <param name="filepath">The path to save to.</param>
        /// <param name="version">The game version to save this file as.</param>
        public void Save(string filepath, FormatVersion version = FormatVersion.Wars)
        {
            // Set up our BINAWriter and write the BINAV2 header.
            HedgeLib.IO.BINAWriter writer = new(File.Create(filepath), Header);

            // Write the HTAP signature.
            writer.WriteSignature(Signature);

            // Write an unknown value that is always 0x200.
            writer.Write(0x200);

            // Write the count of splines in this file.
            writer.Write((ulong)Data.Count);

            // Add an offset to the path table.
            writer.AddOffset("PathTableOffset", 0x08);

            // Fill in the path table offset.
            writer.FillInOffset("PathTableOffset", false);

            // Loop through each path.
            for (int pathIndex = 0; pathIndex < Data.Count; pathIndex++)
            {
                // Add a string for this path's name.
                writer.AddString($"Path{pathIndex}Name", Data[pathIndex].Name, 0x08);

                // Write this path's unknown short value.
                writer.Write(Data[pathIndex].UnknownUShort_1);

                // Write this path's knot count.
                writer.Write((ushort)Data[pathIndex].Knots.Length);

                // Write this path's distance value.
                writer.Write(Data[pathIndex].Distance[^1]);

                // Add an offset to this path's table of unknown, always true, booleans.
                writer.AddOffset($"Path{pathIndex}UnknownBooleanTableOffset", 0x08);

                // Add an offset to this path's distance array.
                writer.AddOffset($"Path{pathIndex}DistanceOffset", 0x08);

                // Add an offset to this path's spline knot array.
                writer.AddOffset($"Path{pathIndex}KnotOffset", 0x08);

                // Add an offset to this path's up vector array.
                writer.AddOffset($"Path{pathIndex}UpVectorOffset", 0x08);

                // Add an offset to this path's forward vector array.
                writer.AddOffset($"Path{pathIndex}ForwardVectorOffset", 0x08);

                // Handle whether or not this spline uses double knots.
                if (Data[pathIndex].DoubleKnots != null)
                {
                    // Write this spline's double knot count.
                    writer.Write((ulong)Data[pathIndex].DoubleKnots.Length);

                    // Add an offset to this path's double spline knot table.
                    writer.AddOffset($"Path{pathIndex}DoubleKnotOffset", 0x08);
                }
                else
                {
                    // Write two zero values in place of the double knot data.
                    writer.Write(0L);
                    writer.Write(0L);
                }

                // Write this path's axis aligned bounding box.
                Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].AxisAlignedBoundingBox.Min);
                Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].AxisAlignedBoundingBox.Max);

                // Write the type count, depending on if this path has a UID or not.
                if (Data[pathIndex].UID != null)
                    writer.Write(2L);
                else
                    writer.Write(1L);

                // Add an offset to this path's type data.
                writer.AddOffset($"Path{pathIndex}TypeOffset", 0x08);
                
                // Write an unknown value that is always 0.
                writer.Write(0L);

                // Add an offset to this path's k-d tree.
                writer.AddOffset($"Path{pathIndex}kdTreeOffset", 0x08);
            }

            // Loop through each path.
            for (int pathIndex = 0; pathIndex < Data.Count; pathIndex++)
            {
                // Fill in the offset for this path's unknown boolean table.
                writer.FillInOffset($"Path{pathIndex}UnknownBooleanTableOffset", false);

                // Write a true value for each knot.
                for (int knotIndex = 0; knotIndex < Data[pathIndex].Knots.Length; knotIndex++)
                    writer.Write(true);

                // Realign to 0x04 bytes.
                writer.FixPadding(0x04);

                // Fill in the offset for this path's distance array.
                writer.FillInOffset($"Path{pathIndex}DistanceOffset", false);

                // Loop through and write each value in this path's distance array.
                for (int distanceIndex = 0; distanceIndex < Data[pathIndex].Distance.Length; distanceIndex++)
                    writer.Write(Data[pathIndex].Distance[distanceIndex]);

                // Fill in the offset for this path's spline knot table.
                writer.FillInOffset($"Path{pathIndex}KnotOffset", false);

                // Loop through and write each spline knot's position.
                for (int knotIndex = 0; knotIndex < Data[pathIndex].Knots.Length; knotIndex++)
                    Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].Knots[knotIndex]);

                // Fill in the offset for this path's up vector array.
                writer.FillInOffset($"Path{pathIndex}UpVectorOffset", false);

                // Loop through and write each value in this path's up vector array.
                for (int upVectorIndex = 0; upVectorIndex < Data[pathIndex].UpVector.Length; upVectorIndex++)
                    Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].UpVector[upVectorIndex]);

                // Fill in the offset for this path's forward vector array.
                writer.FillInOffset($"Path{pathIndex}ForwardVectorOffset", false);

                // Loop through and write each value in this path's forward vector array.
                for (int forwardVectorIndex = 0; forwardVectorIndex < Data[pathIndex].ForwardVector.Length; forwardVectorIndex++)
                    Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].ForwardVector[forwardVectorIndex]);

                // If this path has a double knot spline, then handle it.
                if (Data[pathIndex].DoubleKnots != null)
                {
                    // Fill in the offset for this path's double knot spline.
                    writer.FillInOffset($"Path{pathIndex}DoubleKnotOffset", false);

                    // Loop through and write the position for each double spline knot.
                    for (int doubleKnotIndex = 0; doubleKnotIndex < Data[pathIndex].DoubleKnots.Length; doubleKnotIndex++)
                        Helpers.WriteHedgeLibVector3(writer, Data[pathIndex].DoubleKnots[doubleKnotIndex]);
                }

                // Realign to 0x08.
                writer.FixPadding(0x08);

                // Fill in the offset for this path's type data.
                writer.FillInOffset($"Path{pathIndex}TypeOffset", false);

                // Add the "type" string to the string table.
                writer.AddString($"Path{pathIndex}Type", "type", 0x08);

                // Write an unknown value that is always 0.
                writer.Write(0L);

                // Write this path's type identifier, depending on the format version.
                switch (version)
                {
                    case FormatVersion.Wars: writer.Write((ulong)(SplineTypeWars)Data[pathIndex].Type); break;
                    case FormatVersion.Rangers: writer.Write((ulong)(SplineTypeRangers)Data[pathIndex].Type); break;
                }

                // If this path as a UID, then write the type entry for it as well.
                if (Data[pathIndex].UID != null)
                {
                    // Add the "uid" string to the string table.
                    writer.AddString($"Path{pathIndex}UID", "uid", 0x08);

                    // Write an unknown value that is always 0.
                    writer.Write(0L);

                    // Write this path's UID value.
                    writer.Write((ulong)Data[pathIndex].UID);
                }

                // Fill in the offset for this path's k-d tree.
                writer.FillInOffset($"Path{pathIndex}kdTreeOffset", false);

                #region TODO: Properly write this when the k-d tree is properly reverse engineered.
                writer.Write(Data[pathIndex].KDTree.UnknownUInt32_1);
                writer.Write(Data[pathIndex].KDTree.UnknownUInt32_2);

                writer.AddOffset($"Path{pathIndex}kdTreeUnknownData1_Offset", 0x08);

                writer.Write((ulong)Data[pathIndex].KDTree.UnknownData_2.Count);

                writer.AddOffset($"Path{pathIndex}kdTreeUnknownData2_Offset", 0x08);

                writer.Write((ulong)Data[pathIndex].KDTree.UnknownData_3.Length);

                writer.AddOffset($"Path{pathIndex}kdTreeUnknownData3_Offset", 0x08);

                writer.FillInOffset($"Path{pathIndex}kdTreeUnknownData1_Offset", false);

                writer.Write(Data[pathIndex].KDTree.UnknownData_1);

                writer.FillInOffset($"Path{pathIndex}kdTreeUnknownData2_Offset", false);

                foreach (uint[] value in Data[pathIndex].KDTree.UnknownData_2)
                {
                    writer.Write(value[0]);
                    writer.Write(value[1]);
                }

                writer.FillInOffset($"Path{pathIndex}kdTreeUnknownData3_Offset", false);

                foreach (uint value in Data[pathIndex].KDTree.UnknownData_3)
                    writer.Write(value);
                #endregion
            }

            // Finish writing the BINA information.
            writer.FinishWrite(Header);

            // Close HedgeLib#'s BINAWriter.
            writer.Close();
        }

        /// <summary>
        /// Exports this path's splines to an OBJ file.
        /// </summary>
        /// <param name="filepath">The filepath to export to.</param>
        public void ExportOBJ(string filepath)
        {
            // Set up the StreamWriter.
            StreamWriter obj = new(filepath);

            // Write a comment that we can use on the import function (if the user wants to reimport this OBJ for some reason).
            obj.WriteLine("# KnuxLib PathSpline_WarsRangers OBJ Export");

            // Set up a variable to track vertices.
            int vertexCount = 0;

            // Loop through each path.
            for (int pathIndex = 0; pathIndex < Data.Count; pathIndex++)
            {
                // If this path uses double knots, then write those values.
                if (Data[pathIndex].DoubleKnots != null)
                {
                    // Starting from 0, write each knot value, incrementing by 2 rather than 1.
                    for (int vertexIndex = 0; vertexIndex < Data[pathIndex].DoubleKnots.Length; vertexIndex+=2)
                        obj.WriteLine($"v {Data[pathIndex].DoubleKnots[vertexIndex].X} {Data[pathIndex].DoubleKnots[vertexIndex].Y} {Data[pathIndex].DoubleKnots[vertexIndex].Z}");

                    // Write the remaining knot values, starting from 1 and also incrementing by 2.
                    for (int vertexIndex = 1; vertexIndex < Data[pathIndex].DoubleKnots.Length; vertexIndex+=2)
                        obj.WriteLine($"v {Data[pathIndex].DoubleKnots[vertexIndex].X} {Data[pathIndex].DoubleKnots[vertexIndex].Y} {Data[pathIndex].DoubleKnots[vertexIndex].Z}");
                }
                // If this path doesn't use double knots, then write the regular knot values instead.
                else
                {
                    // Loop through and write each single knot value with no special tricks.
                    for (int vertexIndex = 0; vertexIndex < Data[pathIndex].Knots.Length; vertexIndex++)
                        obj.WriteLine($"v {Data[pathIndex].Knots[vertexIndex].X} {Data[pathIndex].Knots[vertexIndex].Y} {Data[pathIndex].Knots[vertexIndex].Z}");
                }

                // Write this path's name.
                obj.WriteLine($"o {Data[pathIndex].Name}");
                obj.WriteLine($"g {Data[pathIndex].Name}");

                // If this path uses double knots, then write two line objects.
                if (Data[pathIndex].DoubleKnots != null)
                {
                    // Write the first line idenitifer.
                    obj.Write("l ");

                    // Write the first path in this spline.
                    for (int vertexIndex = 0; vertexIndex < Data[pathIndex].DoubleKnots.Length / 2; vertexIndex++)
                        obj.Write($"{vertexIndex + 1 + vertexCount} ");

                    // Write the second line identifier.
                    obj.Write("\r\nl ");

                    // Write the second path in this spline.
                    for (int vertexIndex = Data[pathIndex].DoubleKnots.Length / 2; vertexIndex < Data[pathIndex].DoubleKnots.Length; vertexIndex++)
                        obj.Write($"{vertexIndex + 1 + vertexCount} ");

                    // Write a line break to end the line object.
                    obj.WriteLine();
                }
                else
                {
                    // Write the line idenitifer.
                    obj.Write("l ");

                    // Write the path in this spline.
                    for (int vertexIndex = 0; vertexIndex < Data[pathIndex].Knots.Length; vertexIndex++)
                        obj.Write($"{vertexIndex + 1 + vertexCount} ");

                    // Write a line break to end the line object.
                    obj.WriteLine();
                }

                // Increment the vertexCount based on whether this is a double knotted spline or not.
                if (Data[pathIndex].DoubleKnots != null)
                    vertexCount += Data[pathIndex].DoubleKnots.Length;
                else
                    vertexCount += Data[pathIndex].Knots.Length;
            }

            // Close this StreamWriter.
            obj.Close();
        }

        /// <summary>
        /// Imports an OBJ exported from either 3DS Max or Blender 4.x and converts lines in it to paths.
        /// </summary>
        /// <param name="filepath">The OBJ file to import.</param>
        /// <param name="version">The game version to import this file as.</param>
        public void ImportOBJ(string filepath, FormatVersion version = FormatVersion.Wars)
        {
            // Initialise a path.
            SplinePath path = new() { Name = "" };

            // Set up a list to store coordinates.
            List<Vector3> coordinates = new();

            // Set up a flag to check if a spline is single or double knotted.
            bool doubleKnot = false;

            // Set up a string to identify what exported the OBJ we're reading.
            string? identifier = null;

            // Read the OBJ.
            string[] importedOBJ = File.ReadAllLines(filepath);

            // Set the identifier to "max" if the 3DS Max OBJ Exporter comment (or the KnuxLib one) is present.
            if (importedOBJ[0].Contains("# 3ds Max Wavefront OBJ Exporter") || importedOBJ[0].Contains("# KnuxLib PathSpline_WarsRangers OBJ Export"))
                identifier = "max";

            // Set the identifier to "blender4" if the Blender 4.x comment is present. 
            if (importedOBJ[0].Contains("# Blender 4"))
                identifier = "blender4";

            // Determine how to proceed based on the identifier.
            switch (identifier)
            {
                default:
                    throw new NotSupportedException();
                case "blender4":
                    throw new NotImplementedException();
                case "max":                    
                    // Loop through each line in the OBJ.
                    for (int lineIndex = 0; lineIndex < importedOBJ.Length; lineIndex++)
                    {
                        // If this line is the first vertex entry for an object or the last line in the OBJ, then handle finalising the path.
                        if ((importedOBJ[lineIndex].StartsWith("v ") && !importedOBJ[lineIndex - 1].StartsWith("v ")) || lineIndex == importedOBJ.Length - 1)
                        {
                            // Check if this path actually has a name so we don't save the first, completely empty, one.
                            if (path.Name != "")
                            {
                                // If this isn't a double knotted spline, then fill in the standard knot values.
                                if (!doubleKnot)
                                {
                                    // Initialise the knots array.
                                    path.Knots = new Vector3[coordinates.Count];

                                    // Loop through and write each coordinate value to the knot array.
                                    for (int coordinateIndex = 0; coordinateIndex < coordinates.Count; coordinateIndex++)
                                        path.Knots[coordinateIndex] = coordinates[coordinateIndex];
                                }
                                else
                                {
                                    // Split the coordinates array in half to determine the left and right splines.
                                    List<Vector3> leftSpline = coordinates.Take(coordinates.Count / 2).ToList();
                                    List<Vector3> rightSpline = coordinates.Skip(coordinates.Count / 2).ToList();

                                    // Check that the two splines for this path have the same number of knots.
                                    if (leftSpline.Count != rightSpline.Count)
                                        throw new NotSupportedException($"{path.Name} has a different number of points for its two splines.");

                                    // Initialise the double knots array.
                                    path.DoubleKnots = new Vector3[coordinates.Count];

                                    // Initialise the knots array.
                                    path.Knots = new Vector3[leftSpline.Count];

                                    // Set up an index value to track values for the double knots.
                                    int index = 0;

                                    for (int coordinateIndex = 0; coordinateIndex < leftSpline.Count; coordinateIndex++)
                                    {
                                        // Write the two double knot values.
                                        path.DoubleKnots[index] = leftSpline[coordinateIndex];
                                        path.DoubleKnots[index + 1] = rightSpline[coordinateIndex];

                                        // Increment index by 2 for the next loop.
                                        index += 2;

                                        // Calculate the standard knot value for this pair.
                                        path.Knots[coordinateIndex] = new(
                                                                             (leftSpline[coordinateIndex].X + rightSpline[coordinateIndex].X) / 2,
                                                                             (leftSpline[coordinateIndex].Y + rightSpline[coordinateIndex].Y) / 2,
                                                                             (leftSpline[coordinateIndex].Z + rightSpline[coordinateIndex].Z) / 2
                                                                         );
                                    }

                                    // Initalise the up vector array.
                                    path.UpVector = new Vector3[path.Knots.Length];

                                    // Loop through each knot and calculate its up vector.
                                    for (int knotIndex = 0; knotIndex < path.UpVector.Length - 1; knotIndex++)
                                        path.UpVector[knotIndex] = CalculateDoublePointUpVector(path.DoubleKnots[knotIndex * 2], path.DoubleKnots[knotIndex * 2 + 1], path.DoubleKnots[knotIndex * 2 + 2]);

                                    // Set the last up vector to the same as the one before it.
                                    path.UpVector[^1] = path.UpVector[^2];
                                }

                                // Initialise the distance array.
                                path.Distance = new float[path.Knots.Length];

                                // Loop through each knot to calculate the distance values.
                                for (int knotIndex = 1; knotIndex < path.Knots.Length; knotIndex++)
                                    path.Distance[knotIndex] = Helpers.CalculateDistance(path.Knots[knotIndex - 1], path.Knots[knotIndex]) + path.Distance[knotIndex - 1];


                                // Set up lists to sort the x, y and z values of the coordinates as sorting a Vector3 list doesn't seem possible.
                                List<float> x = new();
                                List<float> y = new();
                                List<float> z = new();

                                // Loop through each coordinate and get the x, y and z values.
                                foreach (var coordinate in coordinates)
                                {
                                    x.Add(coordinate.X);
                                    y.Add(coordinate.Y);
                                    z.Add(coordinate.Z);
                                }

                                // Sort the lists to get the smallest and largest values.
                                x.Sort();
                                y.Sort();
                                z.Sort();

                                // Set up the axis aligned bounding box.
                                path.AxisAlignedBoundingBox.Min = new(x[0], y[0], z[0]);
                                path.AxisAlignedBoundingBox.Max = new(x[^1], y[^1], z[^1]);

                                // Initialise the forward vector array.
                                path.ForwardVector = new Vector3[path.Knots.Length];

                                // Loop through and calculate the forward vectors for each knot (other than the last).
                                for (int knotIndex = 0; knotIndex < path.Knots.Length - 1; knotIndex++)
                                    path.ForwardVector[knotIndex] = Helpers.CalculateForwardVector(path.Knots[knotIndex], path.Knots[knotIndex + 1]);

                                // Set the last knot's forward vector to the same as the one before it.
                                path.ForwardVector[^1] = path.ForwardVector[^2];

                                // If this path isn't double knotted, then handle the up vector stuff here.
                                if (!doubleKnot)
                                {
                                    // Initialise the up vector array.
                                    path.UpVector = new Vector3[path.Knots.Length];

                                    // Loop through and calculate each knot's up vector value based on its forward vector.
                                    for (int knotIndex = 0; knotIndex < path.UpVector.Length; knotIndex++)
                                        path.UpVector[knotIndex] = CalculateSinglePointUpVector(path.ForwardVector[knotIndex]);
                                }

                                #region Shamelessly copying the MaxScript for this as a potential placeholder.
                                uint numberOfLineSegments = (uint)(path.Knots.Length - 1);
                                if (doubleKnot)
                                    numberOfLineSegments = (uint)(path.DoubleKnots.Length - 2);
                                path.KDTree.UnknownUInt32_1 = 0;
                                path.KDTree.UnknownUInt32_2 = 2;
                                path.KDTree.UnknownData_1 = new byte[16] {0, 0, 0, 0,
                                                                          0, 0, 0, 0,
                                                                          3, 0, 0, 0,
                                                                          0, 0, 0, 0};
                                path.KDTree.UnknownData_2.Add(new uint[2] { numberOfLineSegments, 0 });
                                path.KDTree.UnknownData_3 = new uint[numberOfLineSegments];
                                for (uint i = 0; i < numberOfLineSegments; i++)
                                    path.KDTree.UnknownData_3[i] = i;
                                #endregion

                                // Determine the path type based on the end of the name (all of Sonic Team's official splines seem to follow this) and format version.
                                switch (version)
                                {
                                    case FormatVersion.Wars:
                                        if (path.Name.EndsWith("GR"))
                                            path.Type = SplineTypeWars.GrindRail;
                                        else if (path.Name.EndsWith("SV"))
                                            path.Type = SplineTypeWars.SideView;
                                        else
                                            path.Type = SplineTypeWars.Default;
                                        break;

                                    case FormatVersion.Rangers:
                                        if (path.Name.EndsWith("GR"))
                                            path.Type = SplineTypeRangers.GrindRail;
                                        else if (path.Name.EndsWith("SV"))
                                            path.Type = SplineTypeRangers.SideView;
                                        else
                                            path.Type = SplineTypeRangers.Default;
                                        break;
                                }

                                // Save this path.
                                Data.Add(path);

                                // Reset the coordinates list.
                                coordinates = new();

                                // Reset the double knot flag.
                                doubleKnot = false;

                                // Make a new path with an empty name.
                                path = new() { Name = "" };
                            }
                        }

                        // If this line starts with a v and a space, then handle it as a vertex coordinate.
                        if (importedOBJ[lineIndex].StartsWith("v "))
                        {
                            // Split the line on the space.
                            string[] split = importedOBJ[lineIndex].Split(' ');

                            // Parse the last three values in the split as floats and it to the coordinates array.
                            coordinates.Add(new(float.Parse(split[^3]), float.Parse(split[^2]), float.Parse(split[^1])));
                        }

                        // If this line starts with an o and a space, then split it on the space and take the last split as the path name.
                        if (importedOBJ[lineIndex].StartsWith("o "))
                            path.Name = importedOBJ[lineIndex].Split(' ')[^1];

                        // If this line starts with an l and a space and the next line does too, then set the double knot flag to true.
                        if (importedOBJ[lineIndex].StartsWith("l ") && importedOBJ[lineIndex + 1].StartsWith("l "))
                        {
                            // Check if the flag is already set as a ghetto way to detect splines with more than two lines.
                            if (doubleKnot)
                                throw new NotSupportedException($"{path.Name} appears to have more than two lines, this is not supported.");

                            // Set the flag to indicate this spline has two lines.
                            doubleKnot = true;
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Calculates a point's up vector for a single spline.
        /// </summary>
        /// <param name="forwardVector">The point's forward vector to calculate the up vector from.</param>
        /// <returns>The calculated up vector.</returns>
        private static Vector3 CalculateSinglePointUpVector(Vector3 forwardVector) => Vector3.Cross(Vector3.Cross(forwardVector, new(0, 1, 0)), forwardVector);

        /// <summary>
        /// Calculates a point's up vector for a double spline.
        /// </summary>
        /// <param name="pointA">The position of the first point.</param>
        /// <param name="pointB">The position of the point opposite the first one.</param>
        /// <param name="pointC">The position of the point connected to the first one.</param>
        private static Vector3 CalculateDoublePointUpVector(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            // Calculate the forward vector between pointA and pointC.
            Vector3 forwardVector = Helpers.CalculateForwardVector(pointA, pointC);

            // Calculate the forward vector between pointA and pointB to get the right vector.
            var rightVector = Helpers.CalculateForwardVector(pointA, pointB);

            // Cross the right vector with the forward vector and return the result.
            return Vector3.Normalize(Vector3.Cross(rightVector, forwardVector));
        }
    }
}