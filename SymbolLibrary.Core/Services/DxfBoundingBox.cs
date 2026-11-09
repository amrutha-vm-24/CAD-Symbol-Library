using System;
namespace SymbolLibrary.Core.Services
{
    public class DxfBoundingBox
    {
        public double MinX { get; private set; } = double.PositiveInfinity;
        public double MinY { get; private set; } = double.PositiveInfinity;

        public double MaxX { get; private set; } = double.NegativeInfinity;
        public double MaxY { get; private set; } = double.NegativeInfinity;

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;

        public bool HasPoints =>
            !double.IsInfinity(MinX) &&
            !double.IsInfinity(MinY);

        public bool IsValid =>
            HasPoints &&
            Width >= 0 &&
            Height >= 0;

        public void Include(double x, double y)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);

            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }
    }
}