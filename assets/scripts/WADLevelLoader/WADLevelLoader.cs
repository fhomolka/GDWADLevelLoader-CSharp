/*
 * v0.2.1 - Godot 3 script for generate levels from WAD files
 * originally created by Chaosus in 2017-2018, converted to C# by fhomolka in 2020
 * MIT license
 *
 * If you want to extend this script for your purposes, read
 * http://www.gamers.org/dhs/helpdocs/dmsp1666.html
 */
using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;

public class WADLevelLoader : Spatial
{
	[Export] private string WADPath = "e1m1.wad";
	[Export] private string levelName = "E1M1";
	[Export] private float scale = 0.05f;
	[Export] private bool printDebugInfo;

	private SpatialMaterial _surfaceMaterial;
	private File _WADFile;

	private string Decode32AsString(File _file)
	{
		var c1 = (char) _file.Get8();
		var c2 = (char) _file.Get8();
		var c3 = (char) _file.Get8();
		var c4 = (char) _file.Get8();

		char[] charArray = new[] {c1, c2, c3, c4};

		return new string(charArray);
	}

	private string Decode64AsString(File _file)
	{
		var c1 = (char) _file.Get8();
		var c2 = (char) _file.Get8();
		var c3 = (char) _file.Get8();
		var c4 = (char) _file.Get8();
		var c5 = (char) _file.Get8();
		var c6 = (char) _file.Get8();
		var c7 = (char) _file.Get8();
		var c8 = (char) _file.Get8();

		char[] charArray = new[] {c1, c2, c3, c4, c5, c6, c7, c8};

		return new string(charArray);
	}

	private class Header
	{
		public string Type;
		public uint LumpNum;
		public uint DirOffset;
	}

	private class Lump
	{
		public uint Offset;
		public uint Size;
		public string Name;
	}

	private class Thing
	{
		public int X;
		public int Y;
		public int Angle;
		public int Type;
		public int Options;
	}

	private class Linedef
	{
		public int StartVertex;
		public int EndVertex;
		public int Flags;
		public int Type;
		public int Trigger;
		public int RightSidedef;
		public int LeftSidedef;
	}

	private class Sidedef
	{
		public int XOffset;
		public int YOffset;
		public string UpperTexture;
		public string LowerTexture;
		public string MiddleTexture;
		public int Sector;
	}

	private class Vertex
	{
		public float X;
		public float Y;
	}

	public class Segment
	{
		public int From;
		public int To;
		public int Angle;
		public int Linedef;
		public int Direction;
		public int Offset;
	}

	private class SubSector
	{
		public int SegCount;
		public int SegNum;
	}

	private class Node
	{
		public int X;
		public int Y;
		public int DX;
		public int DY;
		public int YUpperRight;
		public int YLowerRight;
		public int XLowerRight;
		public int XUpperRight;
		public int YUpperLeft;
		public int YLowerLeft;
		public int XLowerLeft;
		public int XUpperLeft;
		public int NodeRight;
		public int NodeLeft;
	}

	private class Sector
	{
		public int FloorHeight;
		public int CeilHeight;
		public string FloorTexture;
		public string CeilTexture;
		public int LightLevel;
		public int Special;
		public int Tag;
	}

	private Lump ReadLump(File _file)
	{
		Lump lump = new Lump();
		lump.Offset = _file.Get32();
		lump.Size = _file.Get32();
		lump.Name = Decode64AsString(_file);
		return lump;
	}

	///<summary>
	/// Combine two bytes to short
	/// </summary>
	private int ToShort(byte a, byte b)
	{
		return Mathf.Wrap((b << 8) | (a & 0xff), -32768, 32768);
	}

	///<summary>
	/// Combine eight bytes to string
	/// </summary>
	private string Combine8BytesToString(byte c1, byte c2, byte c3, byte c4, byte c5, byte c6, byte c7, byte c8)
	{
		char[] charArray = new[]
			{(char) c1, (char) c2, (char) c3, (char) c4, (char) c5, (char) c6, (char) c7, (char) c8};
		return new string(charArray);
	}

	private void LoadWAD(string WADPath, string LevelName)
	{
		byte[] buffer;
		int i;
		GD.Print($"Opening {WADPath}...");

		File file = new File();
		file.Open(WADPath, File.ModeFlags.Read);
		if (file.Open(WADPath, File.ModeFlags.Read) != Godot.Error.Ok)
		{
			GD.Print($"Failed to open WAD file {WADPath}");
			return;
		}

		if (printDebugInfo)
		{
			GD.Print("READING HEADER...");
		}

		Header header = new Header();
		header.Type = Decode32AsString(file);
		header.LumpNum = file.Get32();
		header.DirOffset = file.Get32();

		GD.Print($"{this.WADPath} is {header.Type}");

		if (printDebugInfo)
		{
			GD.Print("READING LUMPS");
		}

		Lump lumpMapname = new Lump();
		Lump lumpThings = new Lump();
		Lump lumpLinedefs = new Lump();
		Lump lumpSidedefs = new Lump();
		Lump lumpVertexes = new Lump();
		Lump lumpSegs = new Lump();
		Lump lumpSubsectors = new Lump();
		Lump lumpNodes = new Lump();
		Lump lumpSectors = new Lump();
		Lump lumpReject = new Lump();
		Lump lumpBlockmap = new Lump();

		bool first = true;
		bool breakAfter = false;
		file.Seek(header.DirOffset);
		for (int j = 0; j < header.LumpNum; j++)
		{
			Lump lump = ReadLump(file);
			if (first)
			{
				lumpMapname = lump;
				first = false;
			}

			switch (lump.Name)
			{
				case "THINGS":
					lumpThings = lump;
					break;
				case "LINEDEFS":
					lumpLinedefs = lump;
					break;
				case "SIDEDEFS":
					lumpSidedefs = lump;
					break;
				case "VERTEXES":
					lumpVertexes = lump;
					break;
				case "SEGS":
					lumpSegs = lump;
					break;
				case "SSECTORS":
					lumpSubsectors = lump;
					break;
				case "NODES":
					lumpNodes = lump;
					break;
				case "SECTORS":
					lumpSectors = lump;
					break;
				case "REJECT":
					lumpReject = lump;
					break;
				case "BLOCKMAP":
					lumpBlockmap = lump;
					if (breakAfter)
					{
						break;
					}

					break;
				default:
					if (lump.Name == levelName)
					{
						breakAfter = true;
					}

					break;
			}
		}

		if (printDebugInfo)
		{
			GD.Print($"Internal map name: {lumpMapname.Name}");
		}

		if (printDebugInfo)
		{
			GD.Print($"READING THINGS...");
		}

		file.Seek(lumpThings.Offset);

		buffer = file.GetBuffer((int) lumpThings.Size);
		Thing[] things = new Thing[lumpThings.Size];
		i = 0;
		while (i < buffer.Length)
		{
			Thing thing = new Thing();
			thing.X = ToShort(buffer[i], buffer[i + 1]);
			thing.Y = ToShort(buffer[i + 2], buffer[i + 3]);
			thing.Angle = ToShort(buffer[i + 4], buffer[i + 5]);
			thing.Type = ToShort(buffer[i + 6], buffer[i + 7]);
			thing.Options = ToShort(buffer[i + 8], buffer[i + 9]);
			things.Append(thing);
			i += 10;
		}

		if (printDebugInfo)
		{
			GD.Print("READING LINEDEFS...");
		}

		file.Seek(lumpLinedefs.Offset);
		buffer = file.GetBuffer((int) lumpLinedefs.Size);
		Linedef[] linedefs = new Linedef[lumpLinedefs.Size];
		i = 0;
		int k = 0; 
		while (i < buffer.Length)
		{
			Linedef linedef = new Linedef();
			linedef.StartVertex = ToShort(buffer[i], buffer[i + 1]);
			linedef.EndVertex = ToShort(buffer[i + 2], buffer[i + 3]);
			linedef.Flags = ToShort(buffer[i + 4], buffer[i + 5]);
			linedef.Type = ToShort(buffer[i + 6], buffer[i + 7]);
			linedef.Trigger = ToShort(buffer[i + 8], buffer[i + 9]);
			linedef.RightSidedef = ToShort(buffer[i + 10], buffer[i + 11]);
			linedef.LeftSidedef = ToShort(buffer[i + 12], buffer[i + 13]);
			//linedefs.Append(linedef);
			linedefs[k++] = linedef;
			i += 14;
		}

		if (printDebugInfo)
		{
			GD.Print("READING SIDEDEFS...");
		}

		file.Seek(lumpSidedefs.Offset);
		buffer = file.GetBuffer((int) lumpSidedefs.Size);
		Sidedef[] sieddefs = new Sidedef[lumpSidedefs.Size];
		i = 0;
		while (i < buffer.Length)
		{
			Sidedef sidedef = new Sidedef();
			sidedef.XOffset = ToShort(buffer[i], buffer[i + 1]);
			sidedef.YOffset = ToShort(buffer[i + 2], buffer[i + 3]);
			sidedef.UpperTexture = Combine8BytesToString(buffer[i + 4], buffer[i + 5], buffer[i + 6], buffer[i + 7],
				buffer[i + 8], buffer[i + 9], buffer[i + 10], buffer[i + 11]);
			sidedef.LowerTexture = Combine8BytesToString(buffer[i + 12], buffer[i + 13], buffer[i + 14], buffer[i + 15],
				buffer[i + 16], buffer[i + 17], buffer[i + 18], buffer[i + 19]);
			sidedef.MiddleTexture = Combine8BytesToString(buffer[i + 20], buffer[i + 21], buffer[i + 22],
				buffer[i + 23], buffer[i + 24], buffer[i + 25], buffer[i + 26], buffer[i + 27]);
			sidedef.Sector = ToShort(buffer[i + 28], buffer[i + 29]);
			sieddefs.Append(sidedef);
			i += 30;
		}

		if (printDebugInfo)
		{
			GD.Print("READING VERTEXES...");
		}

		file.Seek(lumpVertexes.Offset);
		buffer = buffer = file.GetBuffer((int) lumpVertexes.Size);
		Vertex[] vertexes = new Vertex[lumpVertexes.Size];
		i = 0;
		k = 0;
		while (i < buffer.Length)
		{
			float x = ToShort(buffer[i], buffer[i + 1]) * scale;
			float y = ToShort(buffer[i + 2], buffer[i + 3]) * scale;
			Vertex vertex = new Vertex();
			vertex.X = x;
			vertex.Y = y;
			vertexes[k] = vertex;
			k += 1;
			i += 4;
		}

		if (printDebugInfo)
		{
			GD.Print("READING SUB-SECTORS...");
		}

		file.Seek(lumpSubsectors.Offset);
		buffer = buffer = file.GetBuffer((int) lumpSubsectors.Size);
		SubSector[] subSectors = new SubSector[lumpSubsectors.Size];
		i = 0;
		while (i < buffer.Length)
		{
			SubSector subSector = new SubSector();
			subSector.SegCount = ToShort(buffer[i], buffer[i + 1]);
			subSector.SegNum = ToShort(buffer[i + 2], buffer[i + 3]);
			subSectors.Append(subSector);
			i += 4;
		}

		if (printDebugInfo)
		{
			GD.Print("READING NODES...");
		}

		file.Seek(lumpNodes.Offset);
		buffer = buffer = file.GetBuffer((int) lumpNodes.Size);
		Node[] nodes = new Node[lumpNodes.Size];
		i = 0;
		while (i < buffer.Length)
		{
			Node node = new Node();
			node.X = ToShort(buffer[i], buffer[i + 1]);
			node.Y = ToShort(buffer[i + 2], buffer[i + 3]);
			node.DX = ToShort(buffer[i + 4], buffer[i + 5]);
			node.DY = ToShort(buffer[i + 6], buffer[i + 7]);
			node.YUpperRight = ToShort(buffer[i + 8], buffer[i + 9]);
			node.YLowerRight = ToShort(buffer[i + 10], buffer[i + 11]);
			node.XLowerRight = ToShort(buffer[i + 12], buffer[i + 13]);
			node.XUpperRight = ToShort(buffer[i + 14], buffer[i + 15]);
			node.YUpperLeft = ToShort(buffer[i + 16], buffer[i + 17]);
			node.YLowerLeft = ToShort(buffer[i + 18], buffer[i + 19]);
			node.XLowerLeft = ToShort(buffer[i + 20], buffer[i + 20]);
			node.XUpperLeft = ToShort(buffer[i + 22], buffer[i + 23]);
			node.NodeRight = ToShort(buffer[i + 24], buffer[i + 25]);
			node.NodeLeft = ToShort(buffer[i + 26], buffer[i + 27]);
			i += 28;
		}

		if (printDebugInfo)
		{
			GD.Print("READING SECTORS...");
		}

		file.Seek(lumpSectors.Offset);
		buffer = buffer = file.GetBuffer((int) lumpSectors.Size);
		Sector[] sectors = new Sector[lumpSectors.Size];
		i = 0;
		while (i < buffer.Length)
		{
			Sector sector = new Sector();
			sector.FloorHeight = ToShort(buffer[i], buffer[i + 1]);
			sector.FloorHeight = ToShort(buffer[i + 1], buffer[i + 3]);
			sector.FloorTexture = Combine8BytesToString(buffer[i + 4], buffer[i + 5], buffer[i + 6], buffer[i + 7],
				buffer[i + 8], buffer[i + 9], buffer[i + 10], buffer[i + 11]);
			sector.CeilTexture = Combine8BytesToString(buffer[i + 12], buffer[i + 13], buffer[i + 14], buffer[i + 15],
				buffer[i + 16], buffer[i + 17], buffer[i + 18], buffer[i + 19]);
			sector.LightLevel = ToShort(buffer[i + 20], buffer[i + 21]);
			sector.Special = ToShort(buffer[i + 22], buffer[i + 23]);
			sector.Tag = ToShort(buffer[i + 24], buffer[i + 25]);
			sectors.Append(sector);
			i += 26;
		}

		file.Close();

		if (printDebugInfo)
		{
			GD.Print("BUILDING GEOMETRY");
		}

		foreach (var ld in linedefs)
		{
			if (ld == null) continue;
			
			Vertex vertex1 = vertexes[ld.StartVertex];
			Vertex vertex2 = vertexes[ld.EndVertex];
			ImmediateGeometry geometry = new ImmediateGeometry();
			geometry.MaterialOverride = _surfaceMaterial;
			geometry.Begin(Mesh.PrimitiveType.Lines);
			if (ld.Type != 0)
			{
				geometry.SetColor(new Color(1, 1, 0));
			}
			else
			{
				geometry.SetColor(new Color(1, 0, 0));
			}
			
			geometry.AddVertex(new Vector3(vertex1.X, 0, vertex1.Y));
			geometry.AddVertex(new Vector3(vertex2.X, 0, vertex2.Y));
			geometry.End();
			AddChild(geometry);
		}
	}
	
	public override void _Ready()
	{
		//base._Ready();
		if (_surfaceMaterial == null)
		{
			_surfaceMaterial = new SpatialMaterial();
			_surfaceMaterial.FlagsUnshaded = true;
			_surfaceMaterial.FlagsVertexLighting = true;
			_surfaceMaterial.VertexColorUseAsAlbedo = true;
		}
		
		LoadWAD(WADPath, levelName);
	}
}




	

