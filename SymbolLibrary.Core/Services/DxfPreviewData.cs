
using System.Collections.Generic;

namespace SymbolLibrary.Core.Services
{
    public class DxfPreviewData
    {
        public List<DxfLineData> Lines { get; set; } = new();

        public List<DxfCircleData> Circles { get; set; } = new();

        public List<DxfArcData> Arcs { get; set; } = new();

        public List<DxfPolylineData> Polylines { get; set; } = new();

        public List<DxfTextData> Texts { get; set; } = new();

        public List<DxfMTextData> MTexts { get; set; } = new();

        public List<DxfInsertData> Inserts { get; set; } = new();
    }

    public class DxfLineData
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
    }

    public class DxfCircleData
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Radius { get; set; }
    }

    public class DxfArcData
    {
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
    }

    public class DxfPolylineData
    {
        public List<DxfPolylinePoint> Points { get; set; } = new();

        public bool IsClosed { get; set; }
    }

    public class DxfPolylinePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Bulge { get; set; }
    }

    public class DxfTextData
    {
        public string Value { get; set; } = string.Empty;

        public double X { get; set; }
        public double Y { get; set; }

        public double Height { get; set; }
        public double Rotation { get; set; }
    }

    public class DxfMTextData
    {
        public string Value { get; set; } = string.Empty;

        public double X { get; set; }
        public double Y { get; set; }

        public double Height { get; set; }
        public double Rotation { get; set; }
    }

    public class DxfInsertData
    {
        public string BlockName { get; set; } = string.Empty;

        public double X { get; set; }
        public double Y { get; set; }

        public double BasePointX { get; set; }
        public double BasePointY { get; set; }

        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;

        public double Rotation { get; set; }

        public DxfPreviewData BlockContent { get; set; } = new();
    }
}
